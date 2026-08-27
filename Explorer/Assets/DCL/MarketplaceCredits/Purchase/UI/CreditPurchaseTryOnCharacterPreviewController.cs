using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterPreview;
using System.Collections.Generic;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    public class CreditPurchaseTryOnCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();

        public CreditPurchaseTryOnCharacterPreviewController(
            CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            World world,
            CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, false, characterPreviewEventBus) { }

        public void TryOnWearable(Avatar avatar, URN purchasedUrn, string? purchasedCategory, IWearableStorage wearableStorage)
        {
            if (string.IsNullOrEmpty(purchasedCategory) && wearableStorage.TryGetElement(purchasedUrn, out IWearable purchasedWearable))
                purchasedCategory = purchasedWearable.GetCategory();

            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
            {
                URN shortenedUrn = urn.Shorten();

                if (shortenedUrn.Equals(purchasedUrn))
                    continue;

                if (!string.IsNullOrEmpty(purchasedCategory)
                    && wearableStorage.TryGetElement(shortenedUrn, out IWearable equippedWearable)
                    && equippedWearable.GetCategory() == purchasedCategory)
                    continue;

                shortenedWearables.Add(shortenedUrn);
            }

            shortenedWearables.Add(purchasedUrn);

            previewAvatarModel.Wearables = shortenedWearables;
            previewAvatarModel.Emotes?.Clear();

            Initialize(avatar, Vector3.zero);
            OnShow();
        }

        public void TryOnEmote(Avatar avatar, URN emoteUrn)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            previewAvatarModel.Wearables = shortenedWearables;
            previewAvatarModel.Emotes ??= new HashSet<URN>();
            previewAvatarModel.Emotes.Clear();
            previewAvatarModel.Emotes.Add(emoteUrn);

            Initialize(avatar, Vector3.zero);
            OnShow();
            PlayEmote(emoteUrn);
        }

        public void ReplayEmote(URN emoteUrn) =>
            PlayEmote(emoteUrn);
    }
}
