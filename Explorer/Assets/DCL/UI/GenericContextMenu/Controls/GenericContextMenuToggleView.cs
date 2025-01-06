using DCL.UI.GenericContextMenu.Controls.Configs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuToggleView : MonoBehaviour, IGenericContextMenuComponent
    {
        [field: SerializeField] public Toggle ToggleComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }

        public void Configure(ContextMenuControlSettings settings)
        {
            ToggleContextMenuControlSettings toggleSettings = settings as ToggleContextMenuControlSettings;
            TextComponent.SetText(toggleSettings!.ToggleText);
        }

        public void UnregisterListeners() =>
            ToggleComponent.onValueChanged.RemoveAllListeners();
    }
}
