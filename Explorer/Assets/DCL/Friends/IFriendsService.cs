using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Friends
{
    public interface IFriendsService : IDisposable
    {
        UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<PaginatedFriendsResult> GetMutualFriendsAsync(string userId, int pageNum, int pageSize, CancellationToken ct);

        UniTask<FriendshipStatus> GetFriendshipStatusAsync(string userId, CancellationToken ct);

        UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask RejectFriendshipAsync(string friendId, CancellationToken ct);

        UniTask CancelFriendshipAsync(string friendId, CancellationToken ct);

        UniTask AcceptFriendshipAsync(string friendId, CancellationToken ct);

        UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct);

        UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken ct);

        UniTask<PaginatedBlockedProfileResult> GetBlockedUsersAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask BlockUserAsync(string userId, CancellationToken ct);

        UniTask UnblockUserAsync(string userId, CancellationToken ct);

        UniTask<UserBlockingStatus> GetUserBlockingStatusAsync(CancellationToken ct);
    }

    public readonly struct PaginatedFriendsResult : IDisposable
    {
        private readonly List<FriendProfile> friends;

        public IReadOnlyList<FriendProfile> Friends => friends;
        public int TotalAmount { get; }

        public PaginatedFriendsResult(IEnumerable<FriendProfile> profiles, int totalAmount)
        {
            friends = ListPool<FriendProfile>.Get();
            friends.AddRange(profiles);
            TotalAmount = totalAmount;
        }

        public void Dispose()
        {
            ListPool<FriendProfile>.Release(friends);
        }
    }

    public readonly struct PaginatedBlockedProfileResult : IDisposable
    {
        private readonly List<BlockedProfile> blockedProfiles;

        public IReadOnlyList<BlockedProfile> BlockedProfiles => blockedProfiles;
        public int TotalAmount { get; }

        public PaginatedBlockedProfileResult(IEnumerable<BlockedProfile> profiles, int totalAmount)
        {
            blockedProfiles = ListPool<BlockedProfile>.Get();
            blockedProfiles.AddRange(profiles);
            TotalAmount = totalAmount;
        }

        public void Dispose()
        {
            ListPool<BlockedProfile>.Release(blockedProfiles);
        }
    }

    public readonly struct UserBlockingStatus : IDisposable
    {
        private readonly List<string> blockedUsers;
        private readonly List<string> blockedByUsers;

        public IReadOnlyList<string> BlockedUsers => blockedUsers;
        public IReadOnlyList<string> BlockedByUsers => blockedByUsers;

        public UserBlockingStatus(IEnumerable<string> blockedUsers, IEnumerable<string> blockedByUsers)
        {
            this.blockedUsers = ListPool<string>.Get();
            this.blockedUsers.AddRange(blockedUsers);

            this.blockedByUsers = ListPool<string>.Get();
            this.blockedByUsers.AddRange(blockedByUsers);
        }

        public void Dispose()
        {
            ListPool<string>.Release(blockedUsers);
            ListPool<string>.Release(blockedByUsers);
        }
    }

    public readonly struct PaginatedFriendRequestsResult : IDisposable
    {
        private readonly List<FriendRequest> requests;

        public IReadOnlyList<FriendRequest> Requests => requests;
        public int TotalAmount { get; }

        public PaginatedFriendRequestsResult(IEnumerable<FriendRequest> requests, int totalAmount)
        {
            this.requests = ListPool<FriendRequest>.Get();
            this.requests.AddRange(requests);
            TotalAmount = totalAmount;
        }

        public void Dispose()
        {
            ListPool<FriendRequest>.Release(requests);
        }
    }
}
