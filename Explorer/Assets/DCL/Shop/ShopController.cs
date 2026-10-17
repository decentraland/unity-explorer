using DCL.UI;
using UnityEngine;

namespace DCL.Shop
{
    /// <summary>
    ///     Placeholder Explore panel section for the in-game Shop: it only shows and hides its view.
    /// </summary>
    public class ShopController : ISection
    {
        private readonly ShopView view;
        private readonly RectTransform rectTransform;

        public ShopController(ShopView view)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
        }

        public void Activate() =>
            view.gameObject.SetActive(true);

        public void Deactivate() =>
            view.gameObject.SetActive(false);

        public void Animate(int triggerId) =>
            view.gameObject.SetActive(triggerId == UIAnimationHashes.IN);

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
