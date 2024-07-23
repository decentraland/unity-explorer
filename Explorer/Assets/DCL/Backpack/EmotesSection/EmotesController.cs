using DCL.Backpack.EmotesSection;
using DCL.Character.CharacterMotion.Components;
using UnityEngine;
using DCL.UI;
using System;

namespace DCL.Backpack
{
    public class EmotesController : ISection, IDisposable
    {
        private readonly RectTransform rectTransform;
        private readonly EmotesView view;
        private readonly BackpackEmoteSlotsController slotsController;
        private readonly BackpackEmoteGridController gridController;

        public EmotesController(EmotesView view,
            BackpackEmoteSlotsController slotsController,
            BackpackEmoteGridController gridController)
        {
            this.view = view;
            this.slotsController = slotsController;
            this.gridController = gridController;

            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Dispose()
        {
            slotsController.Dispose();
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            gridController.Activate();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
            gridController.Deactivate();
        }

        public void Animate(int triggerId)
        {
            view.gameObject.SetActive(triggerId == AnimationHashes.IN);
        }

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
