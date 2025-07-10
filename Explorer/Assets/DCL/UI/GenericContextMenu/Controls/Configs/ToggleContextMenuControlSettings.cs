using DCL.UI.GenericContextMenuParameter;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ToggleContextMenuControlSettings : IContextMenuControlSettings
    {
        internal readonly string toggleText;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly int horizontalLayoutSpacing;
        internal readonly bool horizontalLayoutReverseArrangement;
        internal readonly Action<bool> callback;
        internal bool initialValue;

        /// <summary>
        ///     Toggle component settings for the context menu.
        ///     horizontalLayoutPadding has the default value of (8, 8, 0, 0).
        /// </summary>
        public ToggleContextMenuControlSettings(string toggleText, Action<bool> toggleAction, RectOffset horizontalLayoutPadding = null, int horizontalLayoutSpacing = 30, bool horizontalLayoutReverseArrangement = false)
        {
            this.toggleText = toggleText;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
            this.horizontalLayoutSpacing = horizontalLayoutSpacing;
            this.horizontalLayoutReverseArrangement = horizontalLayoutReverseArrangement;
            this.callback = toggleAction;
        }

        public void SetInitialValue(bool value) =>
            initialValue = value;
    }
}
