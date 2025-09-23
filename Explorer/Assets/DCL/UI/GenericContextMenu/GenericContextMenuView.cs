using DCL.UI.Controls;
using MVC;
using System;
using UnityEngine;

namespace DCL.UI
{
    public class GenericContextMenuView : ViewBase, IView, IDisposable
    {
        [field: SerializeField] public ControlsContainerView ControlsContainer { get; private set; }
        [field: SerializeField] public ButtonWithRightClickHandler BackgroundCloseButton { get; private set; }

        public void Dispose() =>
            BackgroundCloseButton?.Button.onClick.RemoveAllListeners();
    }
}
