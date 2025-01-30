using DCL.Clipboard;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class UserProfileContextMenuControlSettings : IContextMenuControlSettings
    {
        private static readonly RectOffset DEFAULT_HORIZONTAL_LAYOUT_PADDING = new (8, 8, 0, 0);

        public enum FriendshipStatus
        {
            NONE,
            FRIEND,
            REQUEST_SENT,
            REQUEST_RECEIVED,
            BLOCKED,
        }

        internal string userName;
        internal string userAddress;
        internal bool hasClaimedName;
        internal Sprite? userThumbnail;
        internal Color userColor;
        internal FriendshipStatus friendshipStatus;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly ISystemClipboard systemClipboard;
        internal readonly Action<string> requestFriendshipAction;

        public UserProfileContextMenuControlSettings(ISystemClipboard systemClipboard, Action<string> requestFriendshipAction, RectOffset? horizontalLayoutPadding = null)
        {
            this.systemClipboard = systemClipboard;
            this.requestFriendshipAction = requestFriendshipAction;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? DEFAULT_HORIZONTAL_LAYOUT_PADDING;
        }

        public void SetInitialData(string userName,
            string userAddress,
            bool hasClaimedName,
            Color userColor,
            FriendshipStatus friendshipStatus,
            Sprite? userThumbnail = null)
        {
            this.userName = userName;
            this.userAddress = userAddress;
            this.hasClaimedName = hasClaimedName;
            this.userColor = userColor;
            this.userThumbnail = userThumbnail;
            this.friendshipStatus = friendshipStatus;
        }
    }
}
