using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using Decentraland.Social.Friendships;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;

namespace DCL.Friends
{
    public class RPCFriendsService : IFriendsService
    {
        private readonly URLAddress apiUrl;
        private readonly IFriendsEventBus eventBus;
        private readonly ClientWebSocket webSocket = new ();

        public RPCFriendsService(URLAddress apiUrl,
            IFriendsEventBus eventBus)
        {
            this.apiUrl = apiUrl;
            this.eventBus = eventBus;
        }

        public async UniTask<GetPaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            // TODO
            new()
            {
                Friends = Array.Empty<Profile>(),
                TotalAmount = 0,
            };

        public UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<GetPaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            // TODO
            new()
            {
                Requests = Array.Empty<FriendRequest>(),
                TotalAmount = 0,
            };

        public async UniTask<GetPaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            // TODO
            new()
            {
                Requests = Array.Empty<FriendRequest>(),
                TotalAmount = 0,
            };

        public UniTask RejectFriendshipAsync(string friendId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public UniTask CancelFriendshipAsync(string friendId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public UniTask AcceptFriendshipAsync(string friendId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public UniTask DeleteFriendshipAsync(string friendId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public UniTask<FriendRequest> AddFriendshipAsync(string friendId, string messageBody, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public UniTask RemoveFriendAsync(string friendId, CancellationToken ct) =>
            throw new NotImplementedException();

        private async UniTask<IReadOnlyList<string>> GetAllFriendIdsAsync(CancellationToken ct)
        {
            Payload payload = new Payload();

            // TODO: send the request message to the rpc server

            return Array.Empty<string>();
        }

        private async UniTask EnsureConnectionAsync(CancellationToken ct)
        {
            switch (webSocket.State)
            {
                case WebSocketState.Open:
                    return;
                case WebSocketState.Connecting:
                    await UniTask.WaitWhile(() => webSocket.State == WebSocketState.Connecting, cancellationToken: ct);
                    break;
                default:
                    await webSocket.ConnectAsync(new Uri(apiUrl), ct);
                    break;
            }
        }
    }
}
