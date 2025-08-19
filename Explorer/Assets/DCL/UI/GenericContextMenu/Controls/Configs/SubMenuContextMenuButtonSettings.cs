using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenuParameter;
using System.Threading;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class SubMenuContextMenuButtonSettings : IContextMenuControlSettings
    {
        private static readonly Color WHITE_COLOR = new (252f / 255f, 252f / 255f, 252f / 255f, 1f);

        internal readonly string buttonText;
        internal readonly Sprite buttonIcon;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly int horizontalLayoutSpacing;
        internal readonly bool horizontalLayoutReverseArrangement;
        internal readonly Color textColor;
        internal readonly Color iconColor;
        internal readonly GenericContextMenuParameter.GenericContextMenu subMenu;
        internal readonly float anchorPadding;

        public delegate UniTask<bool> VisibilityResolverDelegate(CancellationToken ct);
        public delegate UniTask SettingsFillingDelegate(GenericContextMenuParameter.GenericContextMenu contextSubMenu, CancellationToken ct);

        internal readonly VisibilityResolverDelegate asyncVisibilityResolverDelegate;
        internal readonly SettingsFillingDelegate asyncControlSettingsFillingDelegate;

        public bool IsButtonAsynchronous => asyncVisibilityResolverDelegate != null;
        public bool IsSubMenuAsynchronous => asyncControlSettingsFillingDelegate != null;

        /// <summary>
        ///     Button component settings for the context menu.
        ///     horizontalLayoutPadding has the default value of (8, 8, 0, 0).
        /// </summary>
        public SubMenuContextMenuButtonSettings(string buttonText,
            Sprite buttonIcon,
            GenericContextMenuParameter.GenericContextMenu subMenu,
            float anchorPadding = 24.5f,
            RectOffset horizontalLayoutPadding = null,
            int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false,
            Color textColor = default,
            Color iconColor = default,
            SettingsFillingDelegate asyncControlSettingsFillingDelegate = null,
            VisibilityResolverDelegate asyncVisibilityResolverDelegate = null)
        {
            this.buttonText = buttonText;
            this.buttonIcon = buttonIcon;
            this.subMenu = subMenu;
            this.anchorPadding = anchorPadding;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
            this.horizontalLayoutSpacing = horizontalLayoutSpacing;
            this.horizontalLayoutReverseArrangement = horizontalLayoutReverseArrangement;
            this.textColor = textColor == default(Color) ? WHITE_COLOR : textColor;
            this.iconColor = iconColor == default(Color) ? WHITE_COLOR : iconColor;
            this.asyncControlSettingsFillingDelegate = asyncControlSettingsFillingDelegate;
            this.asyncVisibilityResolverDelegate = asyncVisibilityResolverDelegate;
        }
    }
}
