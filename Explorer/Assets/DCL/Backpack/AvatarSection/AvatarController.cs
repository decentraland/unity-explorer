using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
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
        private readonly BackpackInfoPanelController backpackInfoPanelController;

        public AvatarController(AvatarView view,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            BackpackGridController backpackGridController,
            BackpackInfoPanelController backpackInfoPanelController,
            IThumbnailProvider thumbnailProvider)
        {
            this.backpackInfoPanelController = backpackInfoPanelController;
            this.backpackGridController = backpackGridController;
            new BackpackSearchController(view.backpackSearchBar, backpackCommandBus, backpackEventBus);
            slotsController = new BackpackSlotsController(slotViews, backpackCommandBus, backpackEventBus, rarityBackgrounds, thumbnailProvider);

            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Dispose()
        {
            slotsController?.Dispose();
            backpackInfoPanelController?.Dispose();
        }

        public void RequestInitialWearablesPage()
        {
            backpackGridController.RequestTotalNumber();
        }

        public void Activate() { }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
