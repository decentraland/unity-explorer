using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public abstract class GenericContextMenuButtonWithDelegateView<T> : GenericContextMenuComponentBase
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public void Configure(ButtonWithDelegateContextMenuControlSettings<T> settings)
        {
            TextComponent.SetText(settings.buttonText);
            ImageComponent.sprite = settings.buttonIcon;
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            RegisterListener(settings.callback, settings.data);
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        private void RegisterListener(Delegate listener, T data) =>
            ButtonComponent.onClick.AddListener(() => listener.DynamicInvoke(data));
        public override void RegisterCloseListener(Action listener)
        { }
    }
}
