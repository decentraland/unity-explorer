using JetBrains.Annotations;
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
            DISABLED,
        }

        internal string userName;
        internal string userAddress;
        internal bool hasClaimedName;
        [CanBeNull] internal Uri userThumbnailAddress;
        internal Color userColor;
        internal FriendshipStatus friendshipStatus;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly Action<string, FriendshipStatus> friendButtonClickAction;

        public UserProfileContextMenuControlSettings(Action<string, FriendshipStatus> friendButtonClickAction, RectOffset? horizontalLayoutPadding = null)
        {
            this.friendButtonClickAction = friendButtonClickAction;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? DEFAULT_HORIZONTAL_LAYOUT_PADDING;
        }

        public void SetInitialData(string userName,
            string userAddress,
            bool hasClaimedName,
            Color userColor,
            FriendshipStatus friendshipStatus,
            [CanBeNull] Uri userThumbnailAddress = null)
        {
            this.userName = userName;
            this.userAddress = userAddress;
            this.hasClaimedName = hasClaimedName;
            this.userColor = userColor;
            this.userThumbnailAddress = userThumbnailAddress;
            this.friendshipStatus = friendshipStatus;
        }
    }
}
