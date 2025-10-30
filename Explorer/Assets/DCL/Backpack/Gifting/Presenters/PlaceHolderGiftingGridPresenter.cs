using UnityEngine;

namespace DCL.Backpack.Gifting.Presenters
{
    public class PlaceholderGiftingGridPresenter : IGiftingGridPresenter
    {
        private readonly GameObject gridRoot;
        private readonly RectTransform rectTransform;
        private readonly CanvasGroup canvasGroup;

        public PlaceholderGiftingGridPresenter(GameObject gridRoot)
        {
            this.gridRoot = gridRoot;
            rectTransform = gridRoot.GetComponent<RectTransform>();
            canvasGroup = gridRoot.GetComponent<CanvasGroup>();
        }

        public void Activate()
        {
            gridRoot.SetActive(true);
        }

        public void Deactivate()
        {
            gridRoot.SetActive(false);
        }

        public void SetSearchText(string text)
        {
            /* Logic to be added */
        }

        public RectTransform GetRectTransform()
        {
            return rectTransform;
        }

        public CanvasGroup GetCanvasGroup()
        {
            return canvasGroup;
        }

        public void Dispose() { }
    }
}