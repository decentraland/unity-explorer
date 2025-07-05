using MVC;
using UnityEngine;
using UnityEngine.UI;

public class LivekitHealthGuardView : ViewBase, IView
{

    [field: SerializeField]
    public Button ExitButton { get; private set; } = null!;


}
