using DCL.UI.GenericContextMenu.Controls.Configs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuButtonWithTextView : MonoBehaviour, IGenericContextMenuComponent
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public void Configure(ContextMenuControlSettings settings)
        {
            ButtonContextMenuControlSettings buttonSettings = settings as ButtonContextMenuControlSettings;
            TextComponent.SetText(buttonSettings!.ButtonText);
            ImageComponent.sprite = buttonSettings.ButtonIcon;
        }

        public void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();
    }
}
