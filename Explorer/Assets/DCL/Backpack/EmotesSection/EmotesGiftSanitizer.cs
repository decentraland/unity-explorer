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
    ///     optimistic window of a local gift transfer.
    ///     <para>
    ///         On gift send, if no usable copy of the gifted emote remains, it is removed from every wheel slot
    ///         that holds it. On success the change is committed; on cancel/failure the affected slots are restored.
    ///     </para>
    ///     <para>
    ///         Re-equipping a fully-pending emote is prevented at the backpack grid (items flagged
    ///         <see cref="BackpackItemView.SetIsPending" />), so no equip-time guard is needed here.
    ///     </para>
    /// </summary>
    public sealed class EmotesGiftSanitizer : IDisposable
    {
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteStorage emoteStorage;
        private readonly IOwnedNftFilter ownedNftFilter;
        private readonly BackpackCommandBus commandBus;

        // Per gifted instance URN: the slots we optimistically unequipped, so we can restore them on cancel/failure.
        private readonly Dictionary<string, List<(int slot, IEmote emote)>> unequippedByInstanceUrn = new ();

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

            if (unequipped == null) return;

            unequippedByInstanceUrn[evt.InstanceUrn] = unequipped;

            ReportHub.Log(ReportCategory.GIFTING, $"[EmotesGiftSanitizer] Unequipped emote {baseUrn} after gifting {evt.InstanceUrn}.");
        }

        private void OnSuccess(GiftingEvents.OnSuccessfulGift evt)
        {
            // The gift is confirmed: drop the restore snapshot so the unequip becomes permanent.
            if (!string.IsNullOrEmpty(evt.InstanceUrn))
                unequippedByInstanceUrn.Remove(evt.InstanceUrn);
        }

        private void OnFailed(GiftingEvents.OnFailedGift evt) =>
            Restore(evt.InstanceUrn);

        private void OnCanceled(GiftingEvents.OnCanceledGift evt) =>
            Restore(evt.InstanceUrn);

        private void Restore(string instanceUrn)
        {
            if (string.IsNullOrEmpty(instanceUrn)) return;
            if (!unequippedByInstanceUrn.Remove(instanceUrn, out List<(int slot, IEmote emote)>? unequipped)) return;

            foreach ((int slot, IEmote emote) in unequipped)
            {
                // Don't clobber an emote the user equipped into the slot during the optimistic window.
                if (equippedEmotes.EmoteInSlot(slot) == null)
                    commandBus.SendCommand(new BackpackEquipEmoteCommand(emote.GetUrn().ToString(), slot, false));
            }
        }
    }
}
