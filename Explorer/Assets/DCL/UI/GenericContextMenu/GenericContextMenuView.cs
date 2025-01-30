using MVC;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu
{
    public class GenericContextMenuView : ViewBase, IView, IDisposable
    {
        [field: SerializeField] public RectTransform ControlsContainer { get; private set; }
        [field: SerializeField] public VerticalLayoutGroup ControlsLayoutGroup { get; private set; }
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }

        public void Dispose() =>
            BackgroundCloseButton?.onClick.RemoveAllListeners();
    }
}
