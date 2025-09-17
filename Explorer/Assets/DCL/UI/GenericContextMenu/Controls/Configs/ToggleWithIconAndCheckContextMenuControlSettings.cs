using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ToggleWithIconAndCheckContextMenuControlSettings : ToggleContextMenuControlSettings
    {
        internal readonly ToggleGroup toggleGroup;
        internal readonly Sprite icon;
        internal readonly Color iconColor;

        /// <summary>
        ///     Settings for a toggle control that includes a checkmark and an optional icon.
        /// </summary>
        public ToggleWithIconAndCheckContextMenuControlSettings(
            string toggleText,
            Action<bool> toggleAction,
            Sprite icon = null,
            ToggleGroup toggleGroup = null,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false,
            Color iconColor = default)
            : base(toggleText, toggleAction, null, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement)
        {
            this.toggleGroup = toggleGroup;
            this.icon = icon;
            this.iconColor = iconColor == default ? Color.white : iconColor;
        }
    }
}