using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Friends.UI.FriendPanel;
using DCL.Web3;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Friends
{
    public class FriendOnlineStatusCache : IFriendOnlineStatusCache, IDisposable
    {
        private readonly IFriendsEventBus friendEventBus;
        private readonly Dictionary<FriendProfile, OnlineStatus> friendsOnlineStatus = new ();

        public event Action<FriendProfile>? OnFriendBecameOnline;
        public event Action<FriendProfile>? OnFriendBecameAway;
        public event Action<FriendProfile>? OnFriendBecameOffline;

        public FriendOnlineStatusCache(IFriendsEventBus friendEventBus,
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
            // Remove the friend from the cache by exploiting the hashCode and equals methods of the FriendProfile class which check only the address
            friendsOnlineStatus.Remove(new FriendProfile(new Web3Address(userid), string.Empty, false, URLAddress.EMPTY));

        public OnlineStatus GetFriendStatus(FriendProfile friendProfile) =>
            friendsOnlineStatus.GetValueOrDefault(friendProfile, OnlineStatus.OFFLINE);

        private bool FriendOnlineStatusChanged(FriendProfile friendProfile, OnlineStatus onlineStatus)
        {
            if (friendsOnlineStatus.TryGetValue(friendProfile, out OnlineStatus currentStatus) && currentStatus == onlineStatus)
            {
                ReportHub.Log(LogType.Warning, new ReportData("FriendOnlineStatus"), $"Received duplicate connectivity update for User {friendProfile.Name} with status {onlineStatus}");
                return false;
            }

            friendsOnlineStatus[friendProfile] = onlineStatus;
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
