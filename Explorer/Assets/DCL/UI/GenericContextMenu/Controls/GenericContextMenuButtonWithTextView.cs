using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuButtonWithTextView : GenericContextMenuComponent<ButtonContextMenuControlSettings>
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public override void Configure(ButtonContextMenuControlSettings settings, object initialValue)
        {
            TextComponent.SetText(settings!.ButtonText);
            ImageComponent.sprite = settings.ButtonIcon;
            HorizontalLayoutComponent.padding = settings.HorizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.HorizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.HorizontalLayoutReverseArrangement;
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        public override void RegisterListener(Delegate listener) =>
            ButtonComponent.onClick.AddListener(new UnityAction((Action)listener));

        public override void RegisterCloseListener(Action listener) =>
            RegisterListener(listener);
    }
}
