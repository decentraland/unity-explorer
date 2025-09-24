using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls.Configs
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

        public ToggleWithCheckContextMenuControlSettings(string toggleText,
            Action<bool> toggleAction,
            Sprite toggleIcon,
            ToggleGroup toggleGroup = null,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 30,
            bool horizontalLayoutReverseArrangement = false,
            Color iconColor = default)
            : base(toggleText, toggleAction, toggleIcon, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement, iconColor)
        {
            this.toggleGroup = toggleGroup;
        }
    }
}
