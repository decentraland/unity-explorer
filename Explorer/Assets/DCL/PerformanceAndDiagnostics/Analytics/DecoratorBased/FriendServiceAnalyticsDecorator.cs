using Cysharp.Threading.Tasks;
using DCL.Friends;
using Segment.Serialization;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class FriendServiceAnalyticsDecorator : IFriendsService
    {
        private readonly IFriendsService core;
        private readonly IAnalyticsController analytics;

        public FriendServiceAnalyticsDecorator(IFriendsService core, IAnalyticsController analytics)
        {
            this.core = core;
            this.analytics = analytics;
        }

        public UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            core.GetFriendsAsync(pageNum, pageSize, ct);

        public UniTask<PaginatedFriendsResult> GetMutualFriendsAsync(string userId, int pageNum, int pageSize, CancellationToken ct) =>
            core.GetMutualFriendsAsync(userId, pageNum, pageSize, ct);

        public UniTask<FriendshipStatus> GetFriendshipStatusAsync(string userId, CancellationToken ct) =>
            core.GetFriendshipStatusAsync(userId, ct);

        public UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            core.GetReceivedFriendRequestsAsync(pageNum, pageSize, ct);

        public UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            core.GetSentFriendRequestsAsync(pageNum, pageSize, ct);

        public async UniTask RejectFriendshipAsync(string friendId, CancellationToken ct)
        {
            await core.RejectFriendshipAsync(friendId, ct);

            analytics.Track(AnalyticsEvents.Friends.REQUEST_REJECTED, new JsonObject
            {
                {"receiver_id", friendId}
            });
        }

        public async UniTask CancelFriendshipAsync(string friendId, CancellationToken ct)
        {
            await core.CancelFriendshipAsync(friendId, ct);

            analytics.Track(AnalyticsEvents.Friends.REQUEST_CANCELED, new JsonObject
            {
                {"receiver_id", friendId}
            });
        }

        public async UniTask AcceptFriendshipAsync(string friendId, CancellationToken ct)
        {
            await core.AcceptFriendshipAsync(friendId, ct);

            analytics.Track(AnalyticsEvents.Friends.REQUEST_ACCEPTED, new JsonObject
            {
                {"receiver_id", friendId}
            });
        }

        public async UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct)
        {
            await core.DeleteFriendshipAsync(friendId, ct);

            analytics.Track(AnalyticsEvents.Friends.FRIENDSHIP_DELETED, new JsonObject
            {
                {"receiver_id", friendId}
            });
        }

        public async UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken ct)
        {
            FriendRequest result = await core.RequestFriendshipAsync(friendId, messageBody, ct);

            analytics.Track(AnalyticsEvents.Friends.REQUEST_SENT, new JsonObject
            {
                {"receiver_id", friendId}
            });

            return result;
        }

        public UniTask<PaginatedBlockedProfileResult> GetBlockedUsersAsync(int pageNum, int pageSize, CancellationToken ct) =>
            core.GetBlockedUsersAsync(pageNum, pageSize, ct);

        public async UniTask BlockUserAsync(string userId, CancellationToken ct)
        {
            await core.BlockUserAsync(userId, ct);

            analytics.Track(AnalyticsEvents.Friends.BLOCK_USER, new JsonObject
            {
                {"receiver_id", userId}
            });
        }

        public async UniTask UnblockUserAsync(string userId, CancellationToken ct)
        {
            await core.UnblockUserAsync(userId, ct);

            analytics.Track(AnalyticsEvents.Friends.UNBLOCK_USER, new JsonObject
            {
                {"receiver_id", userId}
            });
        }

        public UniTask<UserBlockingStatus> GetUserBlockingStatusAsync(CancellationToken ct) =>
            core.GetUserBlockingStatusAsync(ct);

        public void Dispose()
        {
            core?.Dispose();
        }
    }
}
