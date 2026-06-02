using System;
using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.Gifting.Events;
using DCL.Diagnostics;
using Utility;

namespace DCL.Backpack.EmotesSection
{
    /// <summary>
    ///     Keeps the equipped emotes (the emote wheel) consistent with the user's actual ownership during the
    ///     optimistic window of a local gift transfer, and prevents equipping an emote whose only owned copy has
    ///     been gifted away.
    ///     <para>
    ///         On gift send, if no usable copy of the gifted emote remains, it is removed from every wheel slot
    ///         that holds it and its base URN is marked as blocked. On success the change is committed; on
    ///         cancel/failure the affected slots are restored and the block is lifted.
    ///     </para>
    ///     <para>
    ///         The block is consulted by <see cref="BackpackBusController" /> to reject equip commands, covering
    ///         both the optimistic window (gifted instance not yet in the pending set) and re-equip attempts that
    ///         come from re-loading a stale profile.
    ///     </para>
    /// </summary>
    public sealed class EmotesGiftSanitizer : IDisposable
    {
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteStorage emoteStorage;
        private readonly IOwnedNftFilter ownedNftFilter;
        private readonly BackpackCommandBus commandBus;

        // Base URNs whose only owned copy is currently being/has been gifted away: equip must stay blocked.
        private readonly HashSet<URN> blockedBaseUrns = new ();

        // Per gifted instance URN: the slots we optimistically unequipped, so we can restore them on cancel/failure.
        private readonly Dictionary<string, GiftSnapshot> snapshotsByInstanceUrn = new ();

        private readonly IDisposable sentSubscription;
        private readonly IDisposable successSubscription;
        private readonly IDisposable failedSubscription;
        private readonly IDisposable canceledSubscription;

        public EmotesGiftSanitizer(
            IEquippedEmotes equippedEmotes,
            IEmoteStorage emoteStorage,
            IOwnedNftFilter ownedNftFilter,
            BackpackCommandBus commandBus,
            IEventBus eventBus)
        {
            this.equippedEmotes = equippedEmotes;
            this.emoteStorage = emoteStorage;
            this.ownedNftFilter = ownedNftFilter;
            this.commandBus = commandBus;

            sentSubscription = eventBus.Subscribe<GiftingEvents.OnSentGift>(OnSent);
            successSubscription = eventBus.Subscribe<GiftingEvents.OnSuccessfulGift>(OnSuccess);
            failedSubscription = eventBus.Subscribe<GiftingEvents.OnFailedGift>(OnFailed);
            canceledSubscription = eventBus.Subscribe<GiftingEvents.OnCanceledGift>(OnCanceled);
        }

        public void Dispose()
        {
            sentSubscription.Dispose();
            successSubscription.Dispose();
            failedSubscription.Dispose();
            canceledSubscription.Dispose();
        }

        /// <summary>
        ///     Returns true when the emote cannot be equipped because its only owned copy has been gifted away.
        /// </summary>
        public bool IsEquipBlocked(IEmote emote)
        {
            URN baseUrn = emote.GetUrn();

            if (blockedBaseUrns.Contains(baseUrn))
                return true;

            // On-chain item: block when no owned instance is still usable (every copy pending a gift).
            // Off-chain/base emotes have no owned-NFT registry and are always equippable.
            if (emoteStorage.TryGetOwnedNftRegistry(baseUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry) && registry.Count > 0)
                return !ownedNftFilter.HasAvailableInstance(registry);

            return false;
        }

        private void OnSent(GiftingEvents.OnSentGift evt)
        {
            if (string.IsNullOrEmpty(evt.InstanceUrn)) return;

            var instanceUrn = new URN(evt.InstanceUrn);
            URN baseUrn = instanceUrn.Shorten();

            // If another usable copy remains after gifting this instance, keep the emote equipped.
            if (emoteStorage.TryGetOwnedNftRegistry(baseUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry)
                && ownedNftFilter.HasAvailableInstance(registry, instanceUrn))
                return;

            List<(int slot, IEmote emote)>? unequipped = null;

            for (var slot = 0; slot < equippedEmotes.SlotCount; slot++)
            {
                IEmote? emote = equippedEmotes.EmoteInSlot(slot);
                if (emote == null || !baseUrn.Equals(emote.GetUrn())) continue;

                unequipped ??= new List<(int slot, IEmote emote)>();
                unequipped.Add((slot, emote));
                commandBus.SendCommand(new BackpackUnEquipEmoteCommand(slot: slot));
            }

            blockedBaseUrns.Add(baseUrn);
            snapshotsByInstanceUrn[evt.InstanceUrn] = new GiftSnapshot(baseUrn, unequipped);

            ReportHub.Log(ReportCategory.GIFTING, $"[EmotesGiftSanitizer] Blocked emote {baseUrn} after gifting {evt.InstanceUrn}.");
        }

        private void OnSuccess(GiftingEvents.OnSuccessfulGift evt)
        {
            // The gift is confirmed: keep the base URN blocked and drop the restore snapshot.
            if (!string.IsNullOrEmpty(evt.InstanceUrn))
                snapshotsByInstanceUrn.Remove(evt.InstanceUrn);
        }

        private void OnFailed(GiftingEvents.OnFailedGift evt) =>
            Restore(evt.InstanceUrn);

        private void OnCanceled(GiftingEvents.OnCanceledGift evt) =>
            Restore(evt.InstanceUrn);

        private void Restore(string instanceUrn)
        {
            if (string.IsNullOrEmpty(instanceUrn)) return;
            if (!snapshotsByInstanceUrn.Remove(instanceUrn, out GiftSnapshot snapshot)) return;

            blockedBaseUrns.Remove(snapshot.BaseUrn);

            if (snapshot.Unequipped == null) return;

            foreach ((int slot, IEmote emote) in snapshot.Unequipped)
            {
                // Don't clobber an emote the user equipped into the slot during the optimistic window.
                if (equippedEmotes.EmoteInSlot(slot) == null)
                    commandBus.SendCommand(new BackpackEquipEmoteCommand(emote.GetUrn().ToString(), slot, false));
            }
        }

        private readonly struct GiftSnapshot
        {
            public readonly URN BaseUrn;
            public readonly List<(int slot, IEmote emote)>? Unequipped;

            public GiftSnapshot(URN baseUrn, List<(int slot, IEmote emote)>? unequipped)
            {
                BaseUrn = baseUrn;
                Unequipped = unequipped;
            }
        }
    }
}
