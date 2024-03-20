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

        public void Activate() { }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
