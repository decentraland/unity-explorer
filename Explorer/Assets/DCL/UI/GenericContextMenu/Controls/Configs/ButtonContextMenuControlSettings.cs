using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ButtonContextMenuControlSettings : IContextMenuControlSettings
    {
        private static readonly Color WHITE_COLOR = new (252f / 255f, 252f / 255f, 252f / 255f, 1f);

        internal readonly string buttonText;
        internal readonly Sprite buttonIcon;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly int horizontalLayoutSpacing;
        internal readonly bool horizontalLayoutReverseArrangement;
        internal readonly Action callback;
        internal readonly Color textColor;
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
            Color iconColor = default)
        {
            this.buttonText = buttonText;
            this.buttonIcon = buttonIcon;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
            this.horizontalLayoutSpacing = horizontalLayoutSpacing;
            this.horizontalLayoutReverseArrangement = horizontalLayoutReverseArrangement;
            this.callback = clickAction;
            this.textColor = textColor == default(Color) ? WHITE_COLOR : textColor;
            this.iconColor = iconColor == default(Color) ? WHITE_COLOR : iconColor;
        }
    }
}
