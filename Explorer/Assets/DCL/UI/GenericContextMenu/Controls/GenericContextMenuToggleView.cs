using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuToggleView : GenericContextMenuComponent<ToggleContextMenuControlSettings>
    {
        [field: SerializeField] public ToggleView ToggleComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }

        public override void Configure(ToggleContextMenuControlSettings settings, object initialValue)
        {
            TextComponent.SetText(settings!.toggleText);
            HorizontalLayoutComponent.padding = settings.horizontalLayoutPadding;
            HorizontalLayoutComponent.spacing = settings.horizontalLayoutSpacing;
            HorizontalLayoutComponent.reverseArrangement = settings.horizontalLayoutReverseArrangement;
            ToggleComponent.Toggle.isOn = initialValue != null && (bool)initialValue;
            ToggleComponent.SetToggleGraphics(ToggleComponent.Toggle.isOn);
            RegisterListener(settings.callback);
        }

        public override void UnregisterListeners() =>
            ToggleComponent.Toggle.onValueChanged.RemoveAllListeners();

        public override void RegisterListener(Delegate listener) =>
            ToggleComponent.Toggle.onValueChanged.AddListener(new UnityAction<bool>((Action<bool>)listener));

        public override void RegisterCloseListener(Action listener) =>
            ToggleComponent.Toggle.onValueChanged.AddListener(_ => listener());
    }
}
