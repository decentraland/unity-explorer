using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuToggleView : GenericContextMenuComponent
    {
        [field: SerializeField] public ToggleView ToggleComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }

        public override void Configure(ContextMenuControlSettings settings, object initialValue)
        {
            ToggleContextMenuControlSettings toggleSettings = settings as ToggleContextMenuControlSettings;
            TextComponent.SetText(toggleSettings!.ToggleText);
            ToggleComponent.Toggle.isOn = initialValue != null && (bool)initialValue;
        }

        public override void UnregisterListeners() =>
            ToggleComponent.Toggle.onValueChanged.RemoveAllListeners();

        public override void RegisterListener(Delegate listener) =>
            ToggleComponent.Toggle.onValueChanged.AddListener(new UnityAction<bool>((Action<bool>)listener));

        public override void RegisterCloseListener(Action listener) =>
            ToggleComponent.Toggle.onValueChanged.AddListener(_ => listener());
    }
}
