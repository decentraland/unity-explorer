using DCL.Profiles;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class BlockUserButtonContextMenuControlSettings : ButtonWithProfileContextMenuControlSettings
    {
        /// <summary>
        ///     OpenUserProfileButton component settings for the context menu.
        ///     horizontalLayoutPadding has the default value of (8, 8, 0, 0).
        /// </summary>
        public BlockUserButtonContextMenuControlSettings(Action<Profile> callback, RectOffset horizontalLayoutPadding = null, int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false) : base(callback, horizontalLayoutPadding, horizontalLayoutSpacing, horizontalLayoutReverseArrangement)
        { }
    }
}
