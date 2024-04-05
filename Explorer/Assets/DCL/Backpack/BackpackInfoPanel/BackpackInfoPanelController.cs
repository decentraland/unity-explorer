using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Backpack.BackpackBus;
using System;
using System.Threading;
using Utility;

namespace DCL.Backpack
{
    public class BackpackInfoPanelController : IDisposable
    {
        private const string DEFAULT_DESCRIPTION = "This wearable does not have a description set.";
        private const int MINIMUM_WAIT_TIME = 500;

        private readonly BackpackInfoPanelView view;
        private readonly BackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO categoryIcons;
        private readonly NftTypeIconSO rarityInfoPanelBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly HideCategoriesController hideCategoriesController;
        private CancellationTokenSource cts;

        public BackpackInfoPanelController(
            BackpackInfoPanelView view,
            BackpackEventBus backpackEventBus,
            NftTypeIconSO categoryIcons,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            NFTColorsSO rarityColors,
            IReadOnlyEquippedWearables equippedWearables)
        {
            this.view = view;
            this.backpackEventBus = backpackEventBus;
            this.categoryIcons = categoryIcons;
            this.rarityInfoPanelBackgrounds = rarityInfoPanelBackgrounds;
            this.rarityColors = rarityColors;

            hideCategoriesController = new HideCategoriesController(
                view.HideCategoryGridView,
                backpackEventBus,
                equippedWearables,
                categoryIcons);

            backpackEventBus.SelectEvent += SetPanelContent;
        }

        public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            await hideCategoriesController.InitialiseAssetsAsync(assetsProvisioner, ct);
        }

        private void SetPanelContent(IWearable wearable)
        {
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            SetInfoPanelStatus(false);
            view.WearableThumbnail.gameObject.SetActive(false);
            view.LoadingSpinner.SetActive(true);
            view.Name.text = wearable.GetName();
            view.Description.text = string.IsNullOrEmpty(wearable.GetDescription()) ? DEFAULT_DESCRIPTION : wearable.GetDescription();
            view.CategoryImage.sprite = categoryIcons.GetTypeImage(wearable.GetCategory());
            view.RarityBackground.sprite = rarityInfoPanelBackgrounds.GetTypeImage(wearable.GetRarity());
            view.RarityBackgroundPanel.color = rarityColors.GetColor(wearable.GetRarity());
            view.RarityName.text = wearable.GetRarity();
            WaitForThumbnailAsync(wearable, cts.Token).Forget();
        }

        private void SetInfoPanelStatus(bool isEmpty)
        {
            view.EmptyPanel.SetActive(isEmpty);
            view.FullPanel.SetActive(!isEmpty);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable, CancellationToken ct)
        {
            do
            {
                await UniTask.Delay(MINIMUM_WAIT_TIME, cancellationToken: ct);
            }
            while (itemWearable.WearableThumbnail == null);

            view.WearableThumbnail.sprite = itemWearable.WearableThumbnail.Value.Asset;
            view.LoadingSpinner.SetActive(false);
            view.WearableThumbnail.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            backpackEventBus.SelectEvent -= SetPanelContent;
        }
    }
}
