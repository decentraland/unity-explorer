using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    [CreateAssetMenu(fileName = "GenericContextMenuSettings", menuName = "SO/ContextMenu/Components/ToggleContextMenuControlSettings")]
    [Serializable]
    public class ToggleContextMenuControlSettings : ContextMenuControlSettings
    {
        [SerializeField] private string toggleText;

        public string ToggleText => toggleText;

        private void OnEnable() =>
            controlTypeType = ContextMenuControlTypes.TOGGLE_WITH_TEXT;
    }
}
