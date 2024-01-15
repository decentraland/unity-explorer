using UnityEngine;
using DCL.UI;

namespace DCL.Backpack
{
    public class EmotesController : ISection
    {
        private readonly RectTransform rectTransform;
        private readonly EmotesView view;

        public EmotesController(EmotesView view)
        {
            this.view = view;

            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Activate() { }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
