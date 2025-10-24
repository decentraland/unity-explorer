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

            var newDebounceInfo = new FriendStatusDebounceInfo (friendProfile, newStatus, new CancellationTokenSource(), DateTime.UtcNow);

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

        private readonly struct FriendStatusDebounceInfo : IEquatable<FriendStatusDebounceInfo>
        {
            public readonly FriendProfile FriendProfile;
            public readonly OnlineStatus NewStatus;
            public readonly CancellationTokenSource CancellationTokenSource;
            private readonly DateTime lastUpdateTime;

            public FriendStatusDebounceInfo(FriendProfile friendProfile, OnlineStatus newStatus, CancellationTokenSource cancellationTokenSource, DateTime lastUpdateTime)
            {
                FriendProfile = friendProfile;
                NewStatus = newStatus;
                CancellationTokenSource = cancellationTokenSource;
                this.lastUpdateTime = lastUpdateTime;
            }

            public bool Equals(FriendStatusDebounceInfo other) =>
                FriendProfile.Address.Equals(other.FriendProfile.Address) &&
                NewStatus == other.NewStatus &&
                lastUpdateTime == other.lastUpdateTime;

            public override bool Equals(object? obj) =>
                obj is FriendStatusDebounceInfo other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(FriendProfile.Address, NewStatus, lastUpdateTime);

            public static bool operator ==(FriendStatusDebounceInfo left, FriendStatusDebounceInfo right) =>
                left.Equals(right);

            public static bool operator !=(FriendStatusDebounceInfo left, FriendStatusDebounceInfo right) =>
                !left.Equals(right);
        }
    }
}
