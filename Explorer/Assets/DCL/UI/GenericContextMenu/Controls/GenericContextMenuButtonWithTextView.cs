using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuButtonWithTextView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public void Configure(ButtonContextMenuControlSettings settings)
        {
            TextComponent.SetText(settings!.buttonText);
            TextComponent.color = settings.textColor;
            ImageComponent.sprite = settings.buttonIcon;
            ImageComponent.color = settings.iconColor;
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            RegisterListener(settings.callback);
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        private void RegisterListener(Action listener) =>
            ButtonComponent.onClick.AddListener(new UnityAction(listener));

        public override void RegisterCloseListener(Action listener) =>
            RegisterListener(listener);
    }
}
