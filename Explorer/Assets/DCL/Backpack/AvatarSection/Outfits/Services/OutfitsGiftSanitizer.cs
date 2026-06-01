using System;
using System.Collections.Generic;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.Gifting.Events;
using Utility;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    /// <summary>
    ///     Keeps the in-memory outfits collection consistent with the user's actual ownership
    ///     during the optimistic window of a local gift transfer. On gift send the gifted full
    ///     URN is removed from every outfit that contains it; on gift success the change is
    ///     committed; on cancel/failure the original wearables list is restored per affected slot.
    /// </summary>
    public sealed class OutfitsGiftSanitizer : IDisposable
    {
        private readonly OutfitsCollection outfitsCollection;
        private readonly Dictionary<string, Dictionary<int, List<string>>> snapshotsByInstanceUrn = new ();

        private readonly IDisposable sentSubscription;
        private readonly IDisposable successSubscription;
        private readonly IDisposable failedSubscription;
        private readonly IDisposable canceledSubscription;

        public OutfitsGiftSanitizer(OutfitsCollection outfitsCollection, IEventBus eventBus)
        {
            this.outfitsCollection = outfitsCollection;

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

            Dictionary<int, List<string>>? perSlotSnapshot = null;

            foreach (OutfitItem item in outfitsCollection.GetAll())
            {
                List<string>? wearables = item?.outfit?.wearables;
                if (wearables == null || !wearables.Contains(evt.InstanceUrn)) continue;

                perSlotSnapshot ??= new Dictionary<int, List<string>>();
                perSlotSnapshot[item!.slot] = new List<string>(wearables);
                wearables.RemoveAll(u => u == evt.InstanceUrn);
            }

            if (perSlotSnapshot != null)
                snapshotsByInstanceUrn[evt.InstanceUrn] = perSlotSnapshot;
        }

        private void OnSuccess(GiftingEvents.OnSuccessfulGift evt)
        {
            if (string.IsNullOrEmpty(evt.InstanceUrn)) return;
            snapshotsByInstanceUrn.Remove(evt.InstanceUrn);
        }

        private void OnFailed(GiftingEvents.OnFailedGift evt) =>
            Restore(evt.InstanceUrn);

        private void OnCanceled(GiftingEvents.OnCanceledGift evt) =>
            Restore(evt.InstanceUrn);

        private void Restore(string instanceUrn)
        {
            if (string.IsNullOrEmpty(instanceUrn)) return;
            if (!snapshotsByInstanceUrn.Remove(instanceUrn, out Dictionary<int, List<string>>? perSlotSnapshot)) return;

            foreach (KeyValuePair<int, List<string>> entry in perSlotSnapshot)
            {
                if (!outfitsCollection.TryGet(entry.Key, out OutfitItem item)) continue;
                if (item?.outfit == null) continue;
                item.outfit.wearables = entry.Value;
            }
        }
    }
}
