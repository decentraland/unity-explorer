using DCL.Backpack.EmotesSection;
using UnityEngine;
using DCL.UI;
using System;

namespace DCL.Backpack
{
    public class EmotesController : ISection, IDisposable
    {
        private static readonly int IN = Animator.StringToHash("In");

        private readonly RectTransform rectTransform;
        private readonly EmotesView view;
        private readonly BackpackEmoteSlotsController slotsController;

        public EmotesController(EmotesView view,
            BackpackEmoteSlotsController slotsController)
        {
            this.view = view;
            this.slotsController = slotsController;

            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Dispose()
        {
            slotsController.Dispose();
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }

        public void Animate(int triggerId)
        {
            view.gameObject.SetActive(triggerId == IN);
        }

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
