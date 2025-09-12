using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ToggleWithCheckContextMenuControlSettings : ToggleContextMenuControlSettings
    {
        internal ToggleGroup toggleGroup;

        public ToggleWithCheckContextMenuControlSettings(string toggleText,
            Action<bool> toggleAction,
            ToggleGroup toggleGroup = null,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 30,
            bool horizontalLayoutReverseArrangement = false)
            : base(toggleText, toggleAction, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement)
        {
            this.toggleGroup = toggleGroup;
        }
    }
}
