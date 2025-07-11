using JetBrains.Annotations;
using DCL.UI.GenericContextMenuParameter;
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

        public struct UserData
        {
            public string userName;
            public string userAddress;
            public bool hasClaimedName;
            [CanBeNull] public Uri userThumbnailAddress;
            public Color userColor;
        }

        internal UserData userData;
        internal FriendshipStatus friendshipStatus;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly Action<UserData, FriendshipStatus> friendButtonClickAction;

        public UserProfileContextMenuControlSettings(Action<UserData, FriendshipStatus> friendButtonClickAction, RectOffset? horizontalLayoutPadding = null)
        {
            this.friendButtonClickAction = friendButtonClickAction;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? DEFAULT_HORIZONTAL_LAYOUT_PADDING;
        }

        public void SetInitialData(UserData data,
            FriendshipStatus friendshipStatus)
        {
            this.userData = data;
            this.friendshipStatus = friendshipStatus;
        }
    }
}
