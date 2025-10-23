using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Utility;

namespace DCL.Friends
{
    public class FriendsConnectivityStatusTracker : IDisposable
    {
        private const int DEBOUNCE_DELAY_MS = 2000;

        private readonly IFriendsEventBus friendEventBus;
        private readonly Dictionary<string, OnlineStatus> friendsOnlineStatus = new ();
        private readonly Dictionary<string, FriendStatusDebounceInfo> debounceInfo = new ();

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

            // Cancel all pending debounce operations
            foreach (var info in debounceInfo.Values)
                info.CancellationTokenSource.SafeCancelAndDispose();

            debounceInfo.Clear();
        }

        private void FriendRemoved(string userid)
        {
            friendsOnlineStatus.Remove(userid);

            // Cancel any pending debounce for this friend
            if (debounceInfo.TryGetValue(userid, out var info))
            {
                info.CancellationTokenSource.SafeCancelAndDispose();
                debounceInfo.Remove(userid);
            }
        }

        public OnlineStatus GetFriendStatus(string friendAddress) =>
            friendsOnlineStatus.GetValueOrDefault(friendAddress, OnlineStatus.OFFLINE);

        private bool FriendOnlineStatusChanged(FriendProfile friendProfile, OnlineStatus onlineStatus)
        {
            if (friendsOnlineStatus.TryGetValue(friendProfile.Address, out OnlineStatus currentStatus) && currentStatus == onlineStatus)
                return false;

            friendsOnlineStatus[friendProfile.Address] = onlineStatus;
            return true;
        }

        private void FriendBecameOnline(FriendProfile friendProfile) =>
            DebounceStatusChange(friendProfile, OnlineStatus.ONLINE, () => OnFriendBecameOnline?.Invoke(friendProfile));

        private void FriendBecameAway(FriendProfile friendProfile) =>
            DebounceStatusChange(friendProfile, OnlineStatus.AWAY, () => OnFriendBecameAway?.Invoke(friendProfile));

        private void FriendBecameOffline(FriendProfile friendProfile) =>
            DebounceStatusChange(friendProfile, OnlineStatus.OFFLINE, () => OnFriendBecameOffline?.Invoke(friendProfile));

        private void DebounceStatusChange(FriendProfile friendProfile, OnlineStatus newStatus, Action onStatusChange)
        {
            string friendAddress = friendProfile.Address;

            if (debounceInfo.TryGetValue(friendAddress, out var existingInfo))
                existingInfo.CancellationTokenSource.SafeCancelAndDispose();

            var cancellationTokenSource = new CancellationTokenSource();
            var newDebounceInfo = new FriendStatusDebounceInfo
            {
                FriendProfile = friendProfile,
                NewStatus = newStatus,
                CancellationTokenSource = cancellationTokenSource,
                LastUpdateTime = DateTime.UtcNow
            };

            this.debounceInfo[friendAddress] = newDebounceInfo;

            DebounceStatusChangeAsync(newDebounceInfo, onStatusChange).Forget();
        }

        private async UniTaskVoid DebounceStatusChangeAsync(FriendStatusDebounceInfo info, Action onStatusChange)
        {
            try
            {
                await UniTask.Delay(DEBOUNCE_DELAY_MS, cancellationToken: info.CancellationTokenSource.Token);

                // Check if this is still the latest status change for this friend
                if (debounceInfo.TryGetValue(info.FriendProfile.Address, out var currentInfo)
                    && currentInfo == info && FriendOnlineStatusChanged(info.FriendProfile, info.NewStatus))
                {
                    onStatusChange();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (debounceInfo.TryGetValue(info.FriendProfile.Address, out var currentInfo)
                    && currentInfo == info)
                {
                    debounceInfo.Remove(info.FriendProfile.Address);
                    info.CancellationTokenSource.SafeCancelAndDispose();
                }
            }
        }

        private struct FriendStatusDebounceInfo : IEquatable<FriendStatusDebounceInfo>
        {
            public FriendProfile FriendProfile;
            public OnlineStatus NewStatus;
            public CancellationTokenSource CancellationTokenSource;
            public DateTime LastUpdateTime;

            public bool Equals(FriendStatusDebounceInfo other) =>
                FriendProfile.Address.Equals(other.FriendProfile.Address) &&
                NewStatus == other.NewStatus &&
                LastUpdateTime == other.LastUpdateTime;

            public override bool Equals(object? obj) =>
                obj is FriendStatusDebounceInfo other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(FriendProfile.Address, NewStatus, LastUpdateTime);

            public static bool operator ==(FriendStatusDebounceInfo left, FriendStatusDebounceInfo right) =>
                left.Equals(right);

            public static bool operator !=(FriendStatusDebounceInfo left, FriendStatusDebounceInfo right) =>
                !left.Equals(right);
        }
    }
}
