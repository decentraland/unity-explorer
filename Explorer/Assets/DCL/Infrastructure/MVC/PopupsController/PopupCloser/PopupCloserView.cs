using UnityEngine;

namespace MVC.PopupsController.PopupCloser
{
    public class PopupCloserView : ViewBase, IPopupCloserView
    {
        [field: SerializeField]
        public ButtonWithRightClickHandler CloseButton { get; private set; }
    }
}
