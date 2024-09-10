using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.ThirdParty;
using DCL.Backpack.BackpackBus;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;

namespace DCL.Backpack
{
    public class BackpackInfoPanelController : IDisposable
    {
        private const string DEFAULT_DESCRIPTION = "This wearable does not have a description set.";
        private const int MINIMUM_WAIT_TIME = 500;
        private const string EMOTE_CATEGORY = "emote";

        private readonly BackpackInfoPanelView view;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO categoryIcons;
        private readonly NftTypeIconSO rarityInfoPanelBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly IThirdPartyNftProviderSource thirdPartyNftProviderSource;
        private readonly HideCategoriesController hideCategoriesController;
        private CancellationTokenSource? cts;

        public BackpackInfoPanelController(
            BackpackInfoPanelView view,
            IBackpackEventBus backpackEventBus,
            NftTypeIconSO categoryIcons,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            NFTColorsSO rarityColors,
            IReadOnlyEquippedWearables equippedWearables,
            AttachmentType attachmentType,
            IThirdPartyNftProviderSource thirdPartyNftProviderSource)
        {
            this.view = view;
            this.backpackEventBus = backpackEventBus;
            this.categoryIcons = categoryIcons;
            this.rarityInfoPanelBackgrounds = rarityInfoPanelBackgrounds;
            this.rarityColors = rarityColors;
            this.thirdPartyNftProviderSource = thirdPartyNftProviderSource;

            hideCategoriesController = new HideCategoriesController(
                view.HideCategoryGridView,
                backpackEventBus,
                equippedWearables,
                categoryIcons);

            if ((attachmentType & AttachmentType.Wearable) != 0)
                backpackEventBus.SelectWearableEvent += SetPanelContent;

            if ((attachmentType & AttachmentType.Emote) != 0)
                backpackEventBus.SelectEmoteEvent += SetPanelContent;
        }

        public void Dispose()
        {
            backpackEventBus.SelectWearableEvent -= SetPanelContent;
            backpackEventBus.SelectEmoteEvent -= SetPanelContent;
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            await hideCategoriesController.InitializeAssetsAsync(assetsProvisioner, ct);
        }

        private void SetPanelContent(IAvatarAttachment wearable)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            SetInfoPanelStatus(false);
            view.WearableThumbnail.gameObject.SetActive(false);
            view.LoadingSpinner.SetActive(true);
            view.Name.text = wearable.GetName();
            view.Description.text = string.IsNullOrEmpty(wearable.GetDescription()) ? DEFAULT_DESCRIPTION : wearable.GetDescription();
            view.CategoryImage.sprite = categoryIcons.GetTypeImage(wearable.GetType() == typeof(Emote) ? EMOTE_CATEGORY : wearable.GetCategory());
            view.RarityBackground.sprite = rarityInfoPanelBackgrounds.GetTypeImage(wearable.GetRarity());
            view.RarityBackgroundPanel.color = rarityColors.GetColor(wearable.GetRarity());
            view.RarityName.text = wearable.GetRarity();

            bool isThirdParty = wearable.IsThirdParty();
            view.RarityBackgroundPanel.gameObject.SetActive(!isThirdParty);
            view.ThirdPartyRarityBackgroundPanel.SetActive(isThirdParty);
            view.ThirdPartyCollectionContainer.SetActive(false);

            if (isThirdParty)
                TrySetThirdPartyProviderNameAsync(wearable, cts.Token).Forget();

            WaitForThumbnailAsync(wearable, cts.Token).Forget();
        }

        private void SetInfoPanelStatus(bool isEmpty)
        {
            view.EmptyPanel.SetActive(isEmpty);
            view.FullPanel.SetActive(!isEmpty);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IAvatarAttachment itemWearable, CancellationToken ct)
        {
            do
            {
                await UniTask.Delay(MINIMUM_WAIT_TIME, cancellationToken: ct);
            }
            while (itemWearable.ThumbnailAssetResult == null);

            view.WearableThumbnail.sprite = itemWearable.ThumbnailAssetResult.Value.Asset;
            view.LoadingSpinner.SetActive(false);
            view.WearableThumbnail.gameObject.SetActive(true);
        }

        private async UniTaskVoid TrySetThirdPartyProviderNameAsync(IAvatarAttachment nft, CancellationToken ct)
        {
            IReadOnlyList<ThirdPartyNftProviderDefinition> tpws = await thirdPartyNftProviderSource.GetAsync(ct);
            URN urn = nft.GetUrn();

            foreach (ThirdPartyNftProviderDefinition tpw in tpws)
            {
                if (!urn.ToString().StartsWith(tpw.urn)) continue;
                view.ThirdPartyCollectionContainer.SetActive(true);
                view.ThirdPartyCollectionName.text = $"Collection <b>{tpw.name}</b>";
                break;
            }
        }

        [Flags]
        public enum AttachmentType
        {
            Wearable = 1 << 1,
            Emote = 1 << 2,
            All = Wearable | Emote,
        }
    }
}
