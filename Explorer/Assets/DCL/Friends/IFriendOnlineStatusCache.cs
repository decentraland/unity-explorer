using DCL.Friends.UI.FriendPanel;
using System;

namespace DCL.Friends
{
    public interface IFriendOnlineStatusCache
    {
        event Action<FriendProfile>? OnFriendBecameOnline;
        event Action<FriendProfile>? OnFriendBecameAway;
        event Action<FriendProfile>? OnFriendBecameOffline;

        OnlineStatus GetFriendStatus(FriendProfile friendProfile);
        OnlineStatus GetFriendStatus(string friendAddress);
    }
}
