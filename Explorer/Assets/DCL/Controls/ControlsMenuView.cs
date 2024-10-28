using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Controls
{
    public class ControlsMenuView : ViewBaseWithAnimationElement, IView
    {

        [field: SerializeField]
        internal Button closeButton { get; private set; } = null!;

    }
}
