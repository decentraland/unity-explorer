using MVC;
using MVC.PopupsController.PopupCloser;
using UnityEngine;
using UnityEngine.UI;

public class PopupCloserView : ViewBase, IPopupCloserView
{
    [field: SerializeField]
    public Button CloseButton { get; private set; }
}
