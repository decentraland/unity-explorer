using System;
using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using Utility;

namespace DCL.Backpack
{
    /// <summary>
    ///     Keeps the equipped wearables consistent with the user's actual ownership during the optimistic window
    ///     of a local gift transfer.
    ///     <para>
    ///         On gift send, if no usable copy of the gifted wearable remains, it is unequipped. On success the
    ///         change is committed; on cancel/failure it is re-equipped.
    ///     </para>
    ///     <para>
    ///         Re-equipping a fully-pending wearable is prevented at the backpack grid (items flagged
    ///         <see cref="BackpackItemView.IsPending" />), so no equip-time guard is needed here.
    ///     </para>
    /// </summary>
    public sealed class WearablesGiftSanitizer : IDisposable
    {
        private readonly IReadOnlyEquippedWearables equippedWearables;
        private readonly IWearableStorage wearableStorage;
        private readonly IOwnedNftFilter ownedNftFilter;
        private readonly BackpackCommandBus commandBus;

        // Per gifted instance URN: the category + wearable we optimistically unequipped, so we can re-equip on cancel/failure.
        private readonly Dictionary<string, (string category, URN baseUrn)> unequippedByInstanceUrn = new ();

        private readonly IDisposable sentSubscription;
        private readonly IDisposable successSubscription;
        private readonly IDisposable failedSubscription;
        private readonly IDisposable canceledSubscription;

        public WearablesGiftSanitizer(
            IReadOnlyEquippedWearables equippedWearables,
            IWearableStorage wearableStorage,
            IOwnedNftFilter ownedNftFilter,
            BackpackCommandBus commandBus,
            IEventBus eventBus)
        {
            this.equippedWearables = equippedWearables;
            this.wearableStorage = wearableStorage;
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

            // If another usable copy remains after gifting this instance, keep the wearable equipped.
            if (wearableStorage.TryGetOwnedNftRegistry(baseUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry)
                && ownedNftFilter.HasAvailableInstance(registry, instanceUrn))
                return;

            foreach ((string category, var wearable) in equippedWearables.Items())
            {
                if (wearable == null || !baseUrn.Equals(wearable.GetUrn())) continue;

                unequippedByInstanceUrn[evt.InstanceUrn] = (category, baseUrn);
                commandBus.SendCommand(new BackpackUnEquipWearableCommand(baseUrn.ToString()));
                ReportHub.Log(ReportCategory.GIFTING, $"[WearablesGiftSanitizer] Unequipped wearable {baseUrn} after gifting {evt.InstanceUrn}.");
                return;
            }
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
            if (!unequippedByInstanceUrn.Remove(instanceUrn, out (string category, URN baseUrn) snapshot)) return;

            // Don't clobber the category if the user equipped something else into it during the optimistic window.
            if (equippedWearables.Wearable(snapshot.category) != null) return;

            commandBus.SendCommand(new BackpackEquipWearableCommand(snapshot.baseUrn.ToString(), false));
        }
    }
}
