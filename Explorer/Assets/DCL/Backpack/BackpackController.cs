using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackController : ISection
    {
        private readonly BackpackView view;
        private readonly RectTransform rectTransform;
        private readonly BackpackSlotsController slotsController;

        public BackpackController(BackpackView view)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            slotsController = new BackpackSlotsController(view.GetComponentsInChildren<AvatarSlotView>());
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
