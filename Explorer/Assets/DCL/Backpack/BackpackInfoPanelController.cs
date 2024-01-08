using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class BackpackInfoPanelController : IDisposable
    {
        private readonly BackpackInfoPanelView view;
        private readonly BackpackEventBus backpackEventBus;
        private readonly NftTypeIconSO categoryIcons;
        private readonly NftTypeIconSO rarityInfoPanelBackgrounds;

        public BackpackInfoPanelController(
            BackpackInfoPanelView view,
            BackpackEventBus backpackEventBus,
            NftTypeIconSO categoryIcons,
            NftTypeIconSO rarityInfoPanelBackgrounds)
        {
            this.view = view;
            this.backpackEventBus = backpackEventBus;
            this.categoryIcons = categoryIcons;
            this.rarityInfoPanelBackgrounds = rarityInfoPanelBackgrounds;

            backpackEventBus.SelectEvent += SetPanelContent;
        }

        private void SetPanelContent(IWearable wearable)
        {
            SetInfoPanelStatus(false);
            view.WearableThumbnail.gameObject.SetActive(false);
            view.LoadingSpinner.SetActive(true);
            view.Name.text = wearable.GetName();
            view.Description.text = wearable.GetDescription();
            view.CategoryImage.sprite = categoryIcons.GetTypeImage(wearable.GetCategory());
            view.RarityBackground.sprite = rarityInfoPanelBackgrounds.GetTypeImage(wearable.GetRarity());
            WaitForThumbnailAsync(wearable).Forget();
        }

        private void SetInfoPanelStatus(bool isEmpty)
        {
            view.EmptyPanel.SetActive(isEmpty);
            view.FullPanel.SetActive(!isEmpty);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IWearable itemWearable)
        {
            do
            {
                await UniTask.Delay(500);
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
