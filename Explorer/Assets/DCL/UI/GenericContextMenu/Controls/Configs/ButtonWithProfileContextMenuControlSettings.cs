using DCL.Profiles;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class ButtonWithProfileContextMenuControlSettings : IContextMenuControlSettings
    {
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly int horizontalLayoutSpacing;
        internal readonly bool horizontalLayoutReverseArrangement;
        internal readonly Action<Profile> callback;
        internal Profile profile;

        /// <summary>
        ///     Button component settings for the context menu. This is reused by other generic buttons that have custom functionality but same settings.
        ///     horizontalLayoutPadding has the default value of (8, 8, 0, 0).
        /// </summary>
        public ButtonWithProfileContextMenuControlSettings(Action<Profile> callback, RectOffset horizontalLayoutPadding = null, int horizontalLayoutSpacing = 10,
            bool horizontalLayoutReverseArrangement = false)
        {
            this.callback = callback;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
            this.horizontalLayoutSpacing = horizontalLayoutSpacing;
            this.horizontalLayoutReverseArrangement = horizontalLayoutReverseArrangement;
        }

        public void SetData(Profile profile)
        {
            this.profile = profile;
        }
    }
}
