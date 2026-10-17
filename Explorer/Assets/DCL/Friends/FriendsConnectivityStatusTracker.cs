using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using UnityEngine.Pool;
using Utility;

namespace DCL.Friends
{
    public class FriendsConnectivityStatusTracker : IDisposable
    {
        private const int DEBOUNCE_DELAY_MS = 2000;

        private readonly IFriendsEventBus friendEventBus;
        private readonly Dictionary<string, TrackedFriend> friendsOnlineStatus = new ();
        private readonly Dictionary<string, FriendStatusDebounceInfo> debounceInfo = new ();

        public event Action<Profile.CompactInfo>? OnFriendBecameOnline;
        public event Action<Profile.CompactInfo>? OnFriendBecameAway;
        public event Action<Profile.CompactInfo>? OnFriendBecameOffline;

        /// <summary>
        ///     Raised with the user id when a friendship ends while the friend was known as online or away: no offline event follows.
        /// </summary>
        public event Action<string>? OnFriendRemoved;

        /// <summary>
        ///     Raised after <see cref="Reset" /> forgot every status: the connectivity snapshot re-arrives as regular events.
        /// </summary>
        public event Action? OnReset;

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

            CancelPendingDebounces();
        }

        /// <summary>
        ///     Forgets every cached status and cancels pending debounces, so subsequent updates
        ///     are evaluated against an empty state and always raise their corresponding event.
        /// </summary>
        public void Reset()
        {
            friendsOnlineStatus.Clear();
            CancelPendingDebounces();
            OnReset?.Invoke();
        }

        /// <summary>
        ///     Appends every friend currently known as online or away.
        /// </summary>
        public void CopyOnlineFriendsTo(List<Profile.CompactInfo> destination)
        {
            foreach (TrackedFriend friend in friendsOnlineStatus.Values)
                if (friend.Status != OnlineStatus.Offline)
                    destination.Add(friend.Profile);
        }

        private void CancelPendingDebounces()
        {
            if (debounceInfo.Count == 0) return;

            using PooledObject<List<FriendStatusDebounceInfo>> scope = ListPool<FriendStatusDebounceInfo>.Get(out List<FriendStatusDebounceInfo> pendingDebounces);
            pendingDebounces.AddRange(debounceInfo.Values);

            // Empty the map before cancelling: cancellation continuations run synchronously and mutate it
            debounceInfo.Clear();

            foreach (FriendStatusDebounceInfo info in pendingDebounces)
                info.CancellationTokenSource.SafeCancelAndDispose();
        }

        private void FriendRemoved(string userid)
        {
            bool wasOnline = friendsOnlineStatus.TryGetValue(userid, out TrackedFriend friend) && friend.Status != OnlineStatus.Offline;
            friendsOnlineStatus.Remove(userid);

            // Cancel any pending debounce for this friend
            if (debounceInfo.TryGetValue(userid, out FriendStatusDebounceInfo info))
            {
                info.CancellationTokenSource.SafeCancelAndDispose();
                debounceInfo.Remove(userid);
            }

            if (wasOnline)
                OnFriendRemoved?.Invoke(userid);
        }

        public OnlineStatus GetFriendStatus(string friendAddress) =>
            friendsOnlineStatus.TryGetValue(friendAddress, out TrackedFriend friend) ? friend.Status : OnlineStatus.Offline;

        private bool FriendOnlineStatusChanged(Profile.CompactInfo friendProfile, OnlineStatus onlineStatus)
        {
            string userId = friendProfile.UserId.Value;

            if (friendsOnlineStatus.TryGetValue(userId, out TrackedFriend current) && current.Status == onlineStatus)
                return false;

            friendsOnlineStatus[userId] = new TrackedFriend(friendProfile, onlineStatus);
            return true;
        }

        private void FriendBecameOnline(Profile.CompactInfo friendProfile) =>
            DebounceStatusChange(friendProfile, OnlineStatus.Online, () => OnFriendBecameOnline?.Invoke(friendProfile));

        private void FriendBecameAway(Profile.CompactInfo friendProfile) =>
            DebounceStatusChange(friendProfile, OnlineStatus.Away, () => OnFriendBecameAway?.Invoke(friendProfile));

        private void FriendBecameOffline(Profile.CompactInfo friendProfile) =>
            DebounceStatusChange(friendProfile, OnlineStatus.Offline, () => OnFriendBecameOffline?.Invoke(friendProfile));

        private void DebounceStatusChange(Profile.CompactInfo friendProfile, OnlineStatus newStatus, Action onStatusChange)
        {
            string friendAddress = friendProfile.UserId;

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
                if (debounceInfo.TryGetValue(info.Profile.UserId, out FriendStatusDebounceInfo currentInfo)
                    && currentInfo == info && FriendOnlineStatusChanged(info.Profile, info.NewStatus))
                {
                    onStatusChange();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (debounceInfo.TryGetValue(info.Profile.UserId, out FriendStatusDebounceInfo currentInfo)
                    && currentInfo == info)
                {
                    debounceInfo.Remove(info.Profile.UserId);
                    info.CancellationTokenSource.SafeCancelAndDispose();
                }
            }
        }

        private readonly struct TrackedFriend
        {
            public readonly Profile.CompactInfo Profile;
            public readonly OnlineStatus Status;

            public TrackedFriend(Profile.CompactInfo profile, OnlineStatus status)
            {
                Profile = profile;
                Status = status;
            }
        }

        private readonly struct FriendStatusDebounceInfo : IEquatable<FriendStatusDebounceInfo>
        {
            public readonly Profile.CompactInfo Profile;
            public readonly OnlineStatus NewStatus;
            public readonly CancellationTokenSource CancellationTokenSource;
            private readonly DateTime lastUpdateTime;

            public FriendStatusDebounceInfo(Profile.CompactInfo profile, OnlineStatus newStatus, CancellationTokenSource cancellationTokenSource, DateTime lastUpdateTime)
            {
                Profile = profile;
                NewStatus = newStatus;
                CancellationTokenSource = cancellationTokenSource;
                this.lastUpdateTime = lastUpdateTime;
            }

            public bool Equals(FriendStatusDebounceInfo other) =>
                Profile.UserId.Equals(other.Profile.UserId) &&
                NewStatus == other.NewStatus &&
                lastUpdateTime == other.lastUpdateTime;

            public override bool Equals(object? obj) =>
                obj is FriendStatusDebounceInfo other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(Profile.UserId, NewStatus, lastUpdateTime);

            public static bool operator ==(FriendStatusDebounceInfo left, FriendStatusDebounceInfo right) =>
                left.Equals(right);

            public static bool operator !=(FriendStatusDebounceInfo left, FriendStatusDebounceInfo right) =>
                !left.Equals(right);
        }
    }
}
