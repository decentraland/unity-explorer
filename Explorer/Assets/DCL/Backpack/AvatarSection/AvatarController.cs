using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
using DCL.Input;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarController : ISection, IDisposable
    {
        private readonly RectTransform rectTransform;
        private readonly BackpackSlotsController slotsController;
        private readonly BackpackGridController backpackGridController;
        private readonly AvatarView view;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackInfoPanelController backpackInfoPanelController;

        public AvatarController(AvatarView view,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            BackpackGridController backpackGridController,
            BackpackInfoPanelController backpackInfoPanelController,
            IThumbnailProvider thumbnailProvider,
            IInputBlock inputBlock)
        {
            this.view = view;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackInfoPanelController = backpackInfoPanelController;
            this.backpackGridController = backpackGridController;
            new BackpackSearchController(view.backpackSearchBar, backpackCommandBus, backpackEventBus, inputBlock);
            slotsController = new BackpackSlotsController(slotViews, backpackCommandBus, backpackEventBus, rarityBackgrounds, thumbnailProvider);

            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Dispose()
        {
            slotsController?.Dispose();
            backpackInfoPanelController?.Dispose();
            backpackGridController?.Dispose();
        }

        public void RequestInitialWearablesPage() =>
            backpackGridController.RequestPage(1, true);

        public void Activate() =>
            backpackGridController.Activate();

        public void Deactivate()
        {
            backpackCommandBus.SendCommand(new BackpackFilterCategoryCommand(""));
            backpackGridController.Deactivate();
        }

        public void Animate(int triggerId) =>
            view.gameObject.SetActive(triggerId == UIAnimationHashes.IN);

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
