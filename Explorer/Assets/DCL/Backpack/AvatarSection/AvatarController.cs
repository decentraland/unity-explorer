using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.UI;
using System;
using DCL.Browser;
using DCL.FeatureFlags;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarController : ISection, IDisposable
    {
        private readonly AvatarView view;
        private readonly RectTransform rectTransform;
        private readonly IWebBrowser webBrowser;
        private readonly BackpackSlotsController slotsController;
        private readonly CategoriesPresenter categoriesPresenter;
        private readonly OutfitsPresenter outfitsPresenter;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackInfoPanelController backpackInfoPanelController;
        private readonly BackpackGridController backpackGridController;
        private readonly AvatarTabsManager tabsManager;

        public AvatarController(AvatarView view,
            FeatureFlagsConfiguration featureFlags,
            IWebBrowser webBrowser,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            BackpackInfoPanelController backpackInfoPanelController,
            BackpackGridController backpackGridController,
            CategoriesPresenter categoriesPresenter,
            OutfitsPresenter outfitsPresenter,
            IThumbnailProvider thumbnailProvider)
        {
            this.view = view;
            this.webBrowser = webBrowser;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackInfoPanelController = backpackInfoPanelController;
            this.backpackGridController = backpackGridController;
            this.categoriesPresenter = categoriesPresenter;
            this.outfitsPresenter = outfitsPresenter;
            
            rectTransform = view.GetComponent<RectTransform>();

            view.marketplaceButton.onClick.AddListener(OnOpenMarketplace);

            slotsController = new BackpackSlotsController(slotViews,
                backpackCommandBus,
                backpackEventBus,
                rarityBackgrounds,
                thumbnailProvider);

            tabsManager = AvatarTabsManager.CreateFromView(
                view,
                categoriesPresenter,
                outfitsPresenter,
                (RectTransform) view.transform);

            bool isOutfitsEnabled = featureFlags.IsEnabled(FeatureFlagsStrings.OUTFITS_ENABLED);
            if (!isOutfitsEnabled)
                tabsManager.SetTabEnabled(AvatarSubSection.Outfits, false);
            
            tabsManager.InitializeAndEnable();
        }

        private void OnOpenMarketplace()
        {
            webBrowser.OpenUrl("https://market.decentraland.org/");
        }

        public void Dispose()
        {
            tabsManager.Dispose();
            slotsController?.Dispose();
            categoriesPresenter?.Dispose();
            outfitsPresenter?.Dispose();
            backpackInfoPanelController?.Dispose();
            view.marketplaceButton.onClick.RemoveAllListeners();
        }

        public void Activate()
        {
            tabsManager.Show();
        }

        public void Deactivate()
        {
            tabsManager.DeactivateAll();
            
            backpackCommandBus.SendCommand(new BackpackFilterCommand(string.Empty,
                AvatarWearableCategoryEnum.Body, string.Empty));

            backpackGridController.Deactivate();
        }

        public void Animate(int triggerId) =>
            view.gameObject.SetActive(triggerId == UIAnimationHashes.IN);

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}