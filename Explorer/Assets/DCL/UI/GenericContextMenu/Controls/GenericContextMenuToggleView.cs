using DCL.UI.GenericContextMenu.Controls.Configs;
using TMPro;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuToggleView : GenericContextMenuComponent
    {
        [field: SerializeField] public ToggleView ToggleComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }

        public override void Configure(ContextMenuControlSettings settings)
        {
            ToggleContextMenuControlSettings toggleSettings = settings as ToggleContextMenuControlSettings;
            TextComponent.SetText(toggleSettings!.ToggleText);
        }

        public override void UnregisterListeners() =>
            ToggleComponent.Toggle.onValueChanged.RemoveAllListeners();
    }
}
