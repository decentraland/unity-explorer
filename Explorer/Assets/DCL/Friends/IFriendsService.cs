using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends
{
    public interface IFriendsService
    {
        UniTask<GetPaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct);

        UniTask<GetPaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask<GetPaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct);

        UniTask RejectFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask CancelFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask AcceptFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask DeleteFriendshipAsync(string friendId, CancellationToken cancellationToken = default);

        UniTask<FriendRequest> AddFriendshipAsync(string friendId, string messageBody, CancellationToken cancellationToken = default);

        UniTask RemoveFriendAsync(string friendId, CancellationToken ct);
    }

    public struct GetPaginatedFriendsResult
    {
        public IReadOnlyList<Profile> Friends;
        public int TotalAmount;
    }

    public struct GetPaginatedFriendRequestsResult
    {
        public IReadOnlyList<FriendRequest> Requests;
        public int TotalAmount;
    }
}
