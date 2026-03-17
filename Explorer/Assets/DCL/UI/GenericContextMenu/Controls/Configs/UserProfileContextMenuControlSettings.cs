using DCL.Profiles;
using DCL.UI.ConfirmationDialog.Opener;
using System;
using UnityEngine;

namespace DCL.UI.Controls.Configs
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

        internal Profile.CompactInfo userData;
        internal FriendshipStatus friendshipStatus;
        internal readonly RectOffset horizontalLayoutPadding;
        internal readonly Action<Profile.CompactInfo, FriendshipStatus> friendButtonClickAction;
        internal readonly bool showProfilePicture;
        internal readonly bool showWalletSection;

        public UserProfileContextMenuControlSettings(Action<Profile.CompactInfo, FriendshipStatus> friendButtonClickAction, RectOffset? horizontalLayoutPadding = null, bool showProfilePicture = true, bool showWalletSection = true)
        {
            this.friendButtonClickAction = friendButtonClickAction;
            this.horizontalLayoutPadding = horizontalLayoutPadding ?? DEFAULT_HORIZONTAL_LAYOUT_PADDING;
            this.showProfilePicture = showProfilePicture;
            this.showWalletSection = showWalletSection;
        }

        public void SetInitialData(Profile.CompactInfo data, FriendshipStatus friendshipStatus)
        {
            this.userData = data;
            this.friendshipStatus = friendshipStatus;
        }
    }
}
