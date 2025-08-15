using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class AsyncSubMenuContextMenuButtonSettings : SubMenuContextMenuButtonSettings
    {
        public AsyncSubMenuContextMenuButtonSettings(string buttonText, Sprite buttonIcon, GenericContextMenuParameter.GenericContextMenu subMenu, float anchorPadding = 24.5f, RectOffset horizontalLayoutPadding = null, int horizontalLayoutSpacing = 10, bool horizontalLayoutReverseArrangement = false, Color textColor = default, Color iconColor = default)
            : base(buttonText, buttonIcon, subMenu, anchorPadding, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement, textColor, iconColor)
        {
        }
    }
}
