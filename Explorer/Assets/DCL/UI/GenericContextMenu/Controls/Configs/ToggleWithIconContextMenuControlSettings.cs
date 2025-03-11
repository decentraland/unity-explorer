using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ToggleWithIconContextMenuControlSettings : ToggleContextMenuControlSettings
    {
        internal readonly Sprite toggleIcon;

        public ToggleWithIconContextMenuControlSettings(Sprite toggleIcon, string toggleText, Action<bool> toggleAction, RectOffset horizontalLayoutPadding = null, int horizontalLayoutSpacing = 30,
            bool horizontalLayoutReverseArrangement = false) : base(toggleText, toggleAction, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement)
        {
            this.toggleIcon = toggleIcon;
        }
    }
}
