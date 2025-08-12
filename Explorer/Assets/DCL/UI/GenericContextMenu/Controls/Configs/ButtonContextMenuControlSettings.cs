using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ButtonContextMenuControlSettings : SimpleButtonContextMenuControlSettings
    {
        internal readonly Sprite buttonIcon;
        internal readonly Color iconColor;

        /// <summary>
        ///     Button component settings for the context menu.
        ///     horizontalLayoutPadding has the default value of (8, 8, 0, 0).
        /// </summary>
        public ButtonContextMenuControlSettings(string buttonText,
            Sprite buttonIcon,
            Action clickAction,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false,
            Color textColor = default,
            Color iconColor = default) : base(buttonText, clickAction, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement, textColor)
        {
            this.buttonIcon = buttonIcon;
            this.iconColor = iconColor == default(Color) ? WHITE_COLOR : iconColor;
        }
    }
}
