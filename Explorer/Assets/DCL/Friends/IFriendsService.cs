using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Friends
{
    public interface IFriendsService : IDisposable
    {
        UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct);

        UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask RejectFriendshipAsync(string friendId, CancellationToken ct);

        UniTask CancelFriendshipAsync(string friendId, CancellationToken ct);

        UniTask AcceptFriendshipAsync(string friendId, CancellationToken ct);

        UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct);

        UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken ct);
    }

    public readonly struct PaginatedFriendsResult : IDisposable
    {
        private readonly List<Profile> friends;

        public IReadOnlyList<Profile> Friends => friends;
        public int TotalAmount { get; }

        public PaginatedFriendsResult(IEnumerable<Profile> profiles, int totalAmount)
        {
            friends = ListPool<Profile>.Get();
            friends.AddRange(profiles);
            TotalAmount = totalAmount;
        }

        public void Dispose()
        {
            ListPool<Profile>.Release(friends);
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
