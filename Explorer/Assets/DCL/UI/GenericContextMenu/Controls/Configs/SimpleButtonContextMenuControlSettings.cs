using DCL.UI.GenericContextMenuParameter;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class SimpleButtonContextMenuControlSettings : IContextMenuControlSettings
    {
        protected static readonly Color WHITE_COLOR = new (252f / 255f, 252f / 255f, 252f / 255f, 1f);

        internal string buttonText;
        internal RectOffset horizontalLayoutPadding;
        internal int horizontalLayoutSpacing;
        internal bool horizontalLayoutReverseArrangement;
        internal Action callback;
        internal Color textColor;

        internal SimpleButtonContextMenuControlSettings() { }

        /// <summary>
        ///     Button component settings for the context menu.
        ///     horizontalLayoutPadding has the default value of (8, 8, 0, 0).
        /// </summary>
        public SimpleButtonContextMenuControlSettings(string buttonText,
            Action clickAction,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false,
            Color textColor = default)
        {
            Configure(buttonText, clickAction, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement, textColor);
        }

        public void Configure(string buttonText,
            Action clickAction,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false,
            Color textColor = default)
        {
            this.buttonText = buttonText;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
            this.horizontalLayoutSpacing = horizontalLayoutSpacing;
            this.horizontalLayoutReverseArrangement = horizontalLayoutReverseArrangement;
            this.callback = clickAction;
            this.textColor = textColor == default(Color) ? WHITE_COLOR : textColor;
        }
    }
}
