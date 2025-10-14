using System;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.Input;
using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class CategoriesPresenter : ISection, IDisposable
    {
        private readonly CategoriesView view;
        private readonly BackpackGridController backpackGridController;
        private readonly BackpackSearchController backpackSearchController;
        private readonly BackpackCommandBus commandBus;

        public CategoriesPresenter(
            CategoriesView view,
            BackpackGridController backpackGridController,
            BackpackCommandBus commandBus,
            IBackpackEventBus backpackEventBus,
            IInputBlock inputBlock
        )
        {
            this.view = view;
            this.backpackGridController = backpackGridController;
            this.commandBus = commandBus;

            // This controller is now responsible for creating its own sub-controller
            backpackSearchController = new BackpackSearchController(view.BackpackSearchBar,
                commandBus,
                backpackEventBus,
                inputBlock);
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            backpackGridController.Activate();
            backpackGridController.RequestPage(1, true);
        }

        public void Deactivate()
        {
            // Reset filters when leaving the tab to ensure a fresh state next time
            commandBus.SendCommand(new BackpackFilterCommand(string.Empty,
                AvatarWearableCategoryEnum.Body,
                string.Empty));

            backpackGridController.Deactivate();
            view.gameObject.SetActive(false);
        }

        #region ISection

        public void Animate(int triggerId)
        {
            view.gameObject.SetActive(triggerId == UIAnimationHashes.IN);
        }

        public void ResetAnimator() { }

        public RectTransform GetRectTransform()
        {
            return (RectTransform)view.transform;
        }

        #endregion

        public void Dispose()
        {
            backpackGridController.Dispose();
        }
    }
}