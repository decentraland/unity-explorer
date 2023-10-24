using MVC;
using UnityEngine;
using UnityEngine.UI;

public class TestPersistentView : ViewBase, IView
{
    [field: SerializeField]
    public Button CloseButton { get; private set; }
}
