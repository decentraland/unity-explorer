using DCL.UI;
using UnityEngine;

namespace DCL.Backpack
{
    public class AvatarController : ISection
    {
        private readonly RectTransform rectTransform;
        private readonly AvatarView view;
        private readonly BackpackSlotsController slotsController;

        public AvatarController(AvatarView view, AvatarSlotView[] slotViews)
        {
            this.view = view;

            slotsController = new BackpackSlotsController(slotViews);
            rectTransform = view.GetComponent<RectTransform>();
        }

        public void Activate() { }

        public void Deactivate() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
