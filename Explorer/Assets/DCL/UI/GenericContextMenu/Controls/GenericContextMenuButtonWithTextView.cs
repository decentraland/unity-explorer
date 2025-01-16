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
            ImageComponent.sprite = settings.buttonIcon;
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            RegisterListener(settings.callback);
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();

        public override void RegisterListener(Delegate listener) =>
            ButtonComponent.onClick.AddListener(new UnityAction((Action)listener));

        public override void RegisterCloseListener(Action listener) =>
            RegisterListener(listener);
    }
}
