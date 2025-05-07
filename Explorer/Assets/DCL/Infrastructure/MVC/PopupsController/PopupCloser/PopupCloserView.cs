using UnityEngine;
using UnityEngine.UI;

namespace MVC.PopupsController.PopupCloser
{
    public class PopupCloserView : ViewBase, IPopupCloserView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }
    }
}
