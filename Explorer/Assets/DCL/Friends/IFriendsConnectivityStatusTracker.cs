using DCL.UI;
using System;

namespace DCL.Friends
{
    public interface IFriendsConnectivityStatusTracker
    {
        event Action<FriendProfile>? OnFriendBecameOnline;
        event Action<FriendProfile>? OnFriendBecameAway;
        event Action<FriendProfile>? OnFriendBecameOffline;

        OnlineStatus GetFriendStatus(string friendAddress);
    }
}
