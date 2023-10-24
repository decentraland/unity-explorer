using MVC;
using UnityEngine;
using UnityEngine.UI;

public class PopupCloserView : ViewBase, IView
{
    [field: SerializeField]
    public Button CloseButton { get; private set; }
}
