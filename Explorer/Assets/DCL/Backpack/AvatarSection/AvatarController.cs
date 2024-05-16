using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
using DCL.Character.CharacterMotion.Components;
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
            BackpackEventBus backpackEventBus,
            BackpackGridController backpackGridController,
            BackpackInfoPanelController backpackInfoPanelController,
            IThumbnailProvider thumbnailProvider,
            DCLInput dclInput)
        {
            this.view = view;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackInfoPanelController = backpackInfoPanelController;
            this.backpackGridController = backpackGridController;
            new BackpackSearchController(view.backpackSearchBar, backpackCommandBus, backpackEventBus, dclInput);
            slotsController = new BackpackSlotsController(slotViews, backpackCommandBus, backpackEventBus, rarityBackgrounds, thumbnailProvider);

            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Dispose()
        {
            slotsController?.Dispose();
            backpackInfoPanelController?.Dispose();
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
            view.gameObject.SetActive(triggerId == AnimationHashes.IN);

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
