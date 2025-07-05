using DCL.UI.GenericContextMenu.Controls.Configs;
using System;

namespace DCL.Communities.CommunitiesCard.Members
{
    public static class FriendshipHelpers
    {
        public static UserProfileContextMenuControlSettings.FriendshipStatus Convert(this FriendshipStatus status)
        {
            return status switch
                   {
                       FriendshipStatus.friend => UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                       FriendshipStatus.request_received => UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED,
                       FriendshipStatus.request_sent => UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT,
                       FriendshipStatus.blocked => UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED,
                       FriendshipStatus.blocked_by => UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED,
                       FriendshipStatus.none => UserProfileContextMenuControlSettings.FriendshipStatus.NONE,
                       _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                   };
        }

        public static FriendshipStatus Convert(this Friends.FriendshipStatus status)
        {
            return status switch
                   {
                       Friends.FriendshipStatus.FRIEND => FriendshipStatus.friend,
                       Friends.FriendshipStatus.REQUEST_RECEIVED => FriendshipStatus.request_received,
                       Friends.FriendshipStatus.REQUEST_SENT => FriendshipStatus.request_sent,
                       Friends.FriendshipStatus.BLOCKED => FriendshipStatus.blocked,
                       Friends.FriendshipStatus.BLOCKED_BY => FriendshipStatus.blocked_by,
                       Friends.FriendshipStatus.NONE => FriendshipStatus.none,
                       _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                   };
        }
    }
}
