using DCL.Clipboard;
using DCL.Profiles;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class UserProfileContextMenuControlSettings : IContextMenuControlSettings
    {
        public enum FriendshipStatus
        {
            NONE,
            FRIEND,
            REQUEST_SENT,
            REQUEST_RECEIVED,
            BLOCKED,
        }

        internal Profile profile;
        internal FriendshipStatus friendshipStatus;
        internal Color userColor;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly ISystemClipboard systemClipboard;
        internal readonly Action<Profile> requestFriendshipAction;

        public UserProfileContextMenuControlSettings(ISystemClipboard systemClipboard, Action<Profile> requestFriendshipAction, RectOffset? horizontalLayoutPadding = null)
        {
            this.systemClipboard = systemClipboard;
            this.requestFriendshipAction = requestFriendshipAction;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? new RectOffset(8, 8, 0, 0);
        }

        public void SetInitialData(Profile profile, Color userColor, FriendshipStatus friendshipStatus)
        {
            this.profile = profile;
            this.userColor = userColor;
            this.friendshipStatus = friendshipStatus;
        }
    }
}
