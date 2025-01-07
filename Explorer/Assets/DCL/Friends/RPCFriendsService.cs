using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using Decentraland.Social.Friendships;
using rpc_csharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;

namespace DCL.Friends
{
    public class RPCFriendsService : IFriendsService
    {
        private readonly IFriendsEventBus eventBus;
        private readonly IProfileRepository profileRepository;
        private readonly WebSocketRpcTransport transport;
        private readonly RpcClient client;
        private readonly List<UniTask<Profile?>> fetchProfileTasks = new ();
        private HashSet<string>? allFriendIds;
        private ClientFriendshipsService? service;

        public RPCFriendsService(URLAddress apiUrl,
            IFriendsEventBus eventBus,
            IProfileRepository profileRepository)
        {
            this.eventBus = eventBus;
            this.profileRepository = profileRepository;
            transport = new WebSocketRpcTransport(new Uri(apiUrl));
            client = new RpcClient(transport);
        }

        public void Dispose()
        {
            transport.Dispose();
            client.Dispose();
        }

        public async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await EnsureDataToBeInitialized(ct);

            var ids = allFriendIds!.Skip((pageNum - 1) * pageSize)
                                   .Take(pageSize);

            fetchProfileTasks.Clear();

            foreach (string userId in ids)
                fetchProfileTasks.Add(profileRepository.GetAsync(userId, ct));

            Profile?[] profiles = (await UniTask.WhenAll(fetchProfileTasks))
                                 .Where(profile => profile != null)
                                 .ToArray();

            return new PaginatedFriendsResult
            {
                Friends = profiles!,
                TotalAmount = allFriendIds!.Count,
            };
        }

        public async UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct)
        {
            await EnsureDataToBeInitialized(ct);

            return allFriendIds!.Contains(friendId);
        }

        public async UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>

            // TODO
            new ()
            {
                Requests = Array.Empty<FriendRequest>(),
                TotalAmount = 0,
            };

        public async UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>

            // TODO
            new ()
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

        public UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public UniTask RemoveFriendAsync(string friendId, CancellationToken ct) =>
            throw new NotImplementedException();

        private async UniTask EnsureDataToBeInitialized(CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            if (allFriendIds == null)
            {
                allFriendIds = new HashSet<string>();
                await FetchAllFriendIdsAsync(allFriendIds, ct);
            }
        }

        private async UniTask FetchAllFriendIdsAsync(HashSet<string> results, CancellationToken ct)
        {
            IUniTaskAsyncEnumerable<UsersResponse> stream = service!.GetFriends(new Payload
            {
                // TODO: synapse token will be replaced by auth-chain
                SynapseToken = "",
            });

            await foreach (var friends in stream.WithCancellation(ct))
            foreach (User friend in friends.Users.Users_)
                results.Add(friend.Address);
        }

        private async UniTask EnsureRpcConnectionAsync(CancellationToken ct)
        {
            switch (transport.State)
            {
                case WebSocketState.Open:
                    return;
                case WebSocketState.Connecting:
                    await UniTask.WaitWhile(() => transport.State == WebSocketState.Connecting, cancellationToken: ct);
                    break;
                default:
                    await transport.ConnectAsync(ct);
                    break;
            }

            if (service == null)
            {
                RpcClientPort port = await client.CreatePort("social-service-port");
                RpcClientModule module = await port.LoadModule(FriendshipsServiceCodeGen.ServiceName);
                service = new ClientFriendshipsService(module);
            }
        }
    }
}
