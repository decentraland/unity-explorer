using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends
{
    public interface IFriendsService : IDisposable
    {
        UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct);

        UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask RejectFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask CancelFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask AcceptFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask DeleteFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken cancellationToken = default);

        UniTask RemoveFriendAsync(string friendId, CancellationToken ct);
    }

    public struct PaginatedFriendsResult
    {
        public IReadOnlyList<Profile> Friends;
        public int TotalAmount;
    }

    public struct PaginatedFriendRequestsResult
    {
        public IReadOnlyList<FriendRequest> Requests;
        public int TotalAmount;
    }
}
