using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackController : ISection
    {
        private readonly BackpackView view;
        private readonly RectTransform rectTransform;

        public BackpackController(BackpackView view)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
