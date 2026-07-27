using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI.Controls.Configs;
using System;

namespace DCL.Communities.CommunitiesCard.Members
{
    public static class FriendshipHelpers
    {
        public static UserProfileContextMenuControlSettings.FriendshipStatus Convert(this FriendshipStatus status)
        {
            return status switch
                   {
                       FriendshipStatus.friend => UserProfileContextMenuControlSettings.FriendshipStatus.Friend,
                       FriendshipStatus.request_received => UserProfileContextMenuControlSettings.FriendshipStatus.RequestReceived,
                       FriendshipStatus.request_sent => UserProfileContextMenuControlSettings.FriendshipStatus.RequestSent,
                       FriendshipStatus.blocked => UserProfileContextMenuControlSettings.FriendshipStatus.Blocked,
                       FriendshipStatus.blocked_by => UserProfileContextMenuControlSettings.FriendshipStatus.Disabled,
                       FriendshipStatus.none => UserProfileContextMenuControlSettings.FriendshipStatus.None,
                       _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                   };
        }

        public static FriendshipStatus Convert(this Friends.FriendshipStatus status)
        {
            return status switch
                   {
                       Friends.FriendshipStatus.Friend => FriendshipStatus.friend,
                       Friends.FriendshipStatus.RequestReceived => FriendshipStatus.request_received,
                       Friends.FriendshipStatus.RequestSent => FriendshipStatus.request_sent,
                       Friends.FriendshipStatus.Blocked => FriendshipStatus.blocked,
                       Friends.FriendshipStatus.BlockedBy => FriendshipStatus.blocked_by,
                       Friends.FriendshipStatus.None => FriendshipStatus.none,
                       _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                   };
        }
    }
}
