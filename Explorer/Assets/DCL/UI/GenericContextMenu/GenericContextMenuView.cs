using DCL.UI.Controls;
using MVC;
using UnityEngine;

namespace DCL.UI
{
    public class GenericContextMenuView : ViewBase, IView
    {
        [field: SerializeField] public ControlsContainerView ControlsContainer { get; private set; }
    }
}
