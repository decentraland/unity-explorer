using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/ButtonContextMenuControlSettings")]
    [Serializable]
    public class ButtonContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private string buttonText;
        [SerializeField] private Sprite buttonIcon;

        public string ButtonText => buttonText;
        public Sprite ButtonIcon => buttonIcon;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.BUTTON_WITH_TEXT_AND_ICON;
    }
}
