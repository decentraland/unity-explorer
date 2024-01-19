using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using Google.Type;
using System;
using System.Threading;
using Utility;
using Color = UnityEngine.Color;

namespace DCL.Backpack
{
    public class BackpackInfoPanelController : IDisposable
    {
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
            IBackpackEquipStatusController backpackEquipStatusController)
        {
            this.view = view;
            this.backpackEventBus = backpackEventBus;
            this.categoryIcons = categoryIcons;
            this.rarityInfoPanelBackgrounds = rarityInfoPanelBackgrounds;
            this.rarityColors = rarityColors;

            hideCategoriesController = new HideCategoriesController(
                view.HideCategoryGridView,
                backpackEventBus,
                backpackEquipStatusController,
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
            view.Description.text = string.IsNullOrEmpty(wearable.GetDescription()) ? "This wearable does not have a description set." : wearable.GetDescription();
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
                await UniTask.Delay(500, cancellationToken: ct);
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
