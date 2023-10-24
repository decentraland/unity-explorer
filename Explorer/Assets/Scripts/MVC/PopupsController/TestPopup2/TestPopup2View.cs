using MVC;
using UnityEngine;
using UnityEngine.UI;

public class TestPopup2View : ViewBase, IView
{
    [field: SerializeField]
    public Button CloseButton { get; private set; }
}
