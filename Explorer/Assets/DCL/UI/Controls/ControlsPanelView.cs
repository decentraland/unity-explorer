using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class ControlsPanelView: ViewBase, IView
    {
        [field: SerializeField] internal Button closeButton { get; private set; } = null!;
    }
}
