using DCL.UI;
using System;
using System.Collections.Generic;

namespace DCL.Friends
{
    public class FriendsConnectivityStatusTracker : IFriendsConnectivityStatusTracker, IDisposable
    {
        private readonly IFriendsEventBus friendEventBus;
        private readonly Dictionary<string, OnlineStatus> friendsOnlineStatus = new ();

        public event Action<FriendProfile>? OnFriendBecameOnline;
        public event Action<FriendProfile>? OnFriendBecameAway;
        public event Action<FriendProfile>? OnFriendBecameOffline;

        public FriendsConnectivityStatusTracker(IFriendsEventBus friendEventBus,
            bool isConnectivityStatusEnabled)
        {
            this.friendEventBus = friendEventBus;

            if (!isConnectivityStatusEnabled) return;

            friendEventBus.OnFriendConnected += FriendBecameOnline;
            friendEventBus.OnFriendAway += FriendBecameAway;
            friendEventBus.OnFriendDisconnected += FriendBecameOffline;

            friendEventBus.OnYouRemovedFriend += FriendRemoved;
            friendEventBus.OnOtherUserRemovedTheFriendship += FriendRemoved;
        }

        public void Dispose()
        {
            friendEventBus.OnFriendConnected -= FriendBecameOnline;
            friendEventBus.OnFriendAway -= FriendBecameAway;
            friendEventBus.OnFriendDisconnected -= FriendBecameOffline;

            friendEventBus.OnYouRemovedFriend -= FriendRemoved;
            friendEventBus.OnOtherUserRemovedTheFriendship -= FriendRemoved;
        }

        private void FriendRemoved(string userid) =>
            friendsOnlineStatus.Remove(userid);

        public OnlineStatus GetFriendStatus(string friendAddress) =>
            friendsOnlineStatus.GetValueOrDefault(friendAddress, OnlineStatus.OFFLINE);

        private bool FriendOnlineStatusChanged(FriendProfile friendProfile, OnlineStatus onlineStatus)
        {
            if (friendsOnlineStatus.TryGetValue(friendProfile.Address, out OnlineStatus currentStatus) && currentStatus == onlineStatus)
                return false;

            friendsOnlineStatus[friendProfile.Address] = onlineStatus;
            return true;
        }

        private void FriendBecameOnline(FriendProfile friendProfile)
        {
            if (FriendOnlineStatusChanged(friendProfile, OnlineStatus.ONLINE))
                OnFriendBecameOnline?.Invoke(friendProfile);
        }

        private void FriendBecameAway(FriendProfile friendProfile)
        {
            if (FriendOnlineStatusChanged(friendProfile, OnlineStatus.AWAY))
                OnFriendBecameAway?.Invoke(friendProfile);
        }

        private void FriendBecameOffline(FriendProfile friendProfile)
        {
            if (FriendOnlineStatusChanged(friendProfile, OnlineStatus.OFFLINE))
                OnFriendBecameOffline?.Invoke(friendProfile);
        }
    }
}
