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

        public event Action BackgroundCloseButtonClicked;

        private void Awake()
        {
            BackgroundCloseButton?.onClick.AddListener(() => BackgroundCloseButtonClicked?.Invoke());
        }

        public void Dispose()
        {
            BackgroundCloseButton?.onClick.RemoveAllListeners();
        }
    }
}
