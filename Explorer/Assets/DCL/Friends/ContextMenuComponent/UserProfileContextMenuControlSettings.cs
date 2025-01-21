using DCL.Clipboard;
using DCL.Profiles;
using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;

namespace DCL.Friends.ContextMenuComponent
{
    public class UserProfileContextMenuControlSettings : IContextMenuControlSettings
    {
        internal Profile profile;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly IFriendsService friendsService;
        internal readonly ISystemClipboard systemClipboard;

        public UserProfileContextMenuControlSettings(IFriendsService friendsService, ISystemClipboard systemClipboard, RectOffset? horizontalLayoutPadding = null)
        {
            this.friendsService = friendsService;
            this.systemClipboard = systemClipboard;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
        }

        public void SetProfile(Profile profile) =>
            this.profile = profile;
    }
}
