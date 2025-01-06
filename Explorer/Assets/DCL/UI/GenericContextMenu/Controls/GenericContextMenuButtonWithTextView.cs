using DCL.UI.GenericContextMenu.Controls.Configs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuButtonWithTextView : GenericContextMenuComponent
    {
        [field: SerializeField] public Button ButtonComponent { get; private set; }
        [field: SerializeField] public TMP_Text TextComponent { get; private set; }
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public override void Configure(ContextMenuControlSettings settings)
        {
            ButtonContextMenuControlSettings buttonSettings = settings as ButtonContextMenuControlSettings;
            TextComponent.SetText(buttonSettings!.ButtonText);
            ImageComponent.sprite = buttonSettings.ButtonIcon;
        }

        public override void UnregisterListeners() =>
            ButtonComponent.onClick.RemoveAllListeners();
    }
}
