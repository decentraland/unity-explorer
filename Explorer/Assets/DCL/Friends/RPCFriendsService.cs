using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Decentraland.SocialService.V3;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
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
        private const int TIMEOUT_SECONDS = 30;
        private const string RPC_SERVICE_NAME = "SocialService";
        private const string GET_FRIENDS_PROCEDURE_NAME = "GetFriends";
        private const string GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME = "GetSentFriendshipRequests";
        private const string GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME = "GetPendingFriendshipRequests";
        private const string GET_FRIENDSHIP_STATUS_PROCEDURE_NAME = "GetFriendshipStatus";
        private const string UPDATE_FRIENDSHIP_PROCEDURE_NAME = "UpsertFriendship";
        private const string SUBSCRIBE_FRIENDSHIP_UPDATES_PROCEDURE_NAME = "SubscribeToFriendshipUpdates";
        private const string GET_MUTUAL_FRIENDS_PROCEDURE_NAME = "GetMutualFriends";

        private readonly IFriendsEventBus eventBus;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly FriendsCache friendsCache;
        private readonly WebSocketRpcTransport transport;
        private readonly RpcClient client;
        private readonly List<UniTask<Profile?>> fetchProfileTasks = new ();
        private readonly List<FriendRequest> friendRequestsBuffer = new ();
        private readonly Dictionary<string, string> authChainBuffer = new ();
        private readonly HashSet<string> allFriendsBuffer = new ();

        private RpcClientModule? module;

        public RPCFriendsService(URLAddress apiUrl,
            IFriendsEventBus eventBus,
            IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache,
            FriendsCache friendsCache)
        {
            this.eventBus = eventBus;
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            this.friendsCache = friendsCache;
            transport = new WebSocketRpcTransport(new Uri(apiUrl));
            client = new RpcClient(transport);
        }

        public void Dispose()
        {
            transport.Dispose();
            client.Dispose();
        }

        public async UniTask SubscribeToIncomingFriendshipEventsAsync(CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            IUniTaskAsyncEnumerable<FriendshipUpdate> stream =
                module!.CallServerStream<FriendshipUpdate>(SUBSCRIBE_FRIENDSHIP_UPDATES_PROCEDURE_NAME, new Empty());

            await foreach (var response in stream.WithCancellation(ct))
            {
                switch (response.UpdateCase)
                {
                    case FriendshipUpdate.UpdateOneofCase.Accept:
                        friendsCache.Add(response.Accept.User.Address);
                        eventBus.BroadcastFriendRequestAccepted(response.Accept.User.Address);
                        break;

                    case FriendshipUpdate.UpdateOneofCase.Cancel:
                        eventBus.BroadcastFriendRequestCanceled(response.Cancel.User.Address);
                        break;

                    case FriendshipUpdate.UpdateOneofCase.Delete:
                        friendsCache.Remove(response.Delete.User.Address);
                        eventBus.BroadcastFriendRequestRemoved(response.Delete.User.Address);
                        break;

                    case FriendshipUpdate.UpdateOneofCase.Reject:
                        eventBus.BroadcastFriendRequestRejected(response.Reject.User.Address);
                        break;

                    case FriendshipUpdate.UpdateOneofCase.Request:
                        var request = response.Request;

                        var fr = new FriendRequest(
                            GetFriendRequestId(request.User.Address, request.CreatedAt),
                            DateTimeOffset.FromUnixTimeSeconds(request.CreatedAt).DateTime,
                            request.User.Address,
                            identityCache.EnsuredIdentity().Address,
                            request.HasMessage ? request.Message : string.Empty);

                        eventBus.BroadcastFriendRequestReceived(fr);
                        break;
                }
            }
        }

        public async UniTask<PaginatedFriendsResult> GetOnlineFriendsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendsAsync(pageNum, pageSize, ConnectivityStatus.Online, ct);

        public async UniTask<PaginatedFriendsResult> GetOfflineFriendsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendsAsync(pageNum, pageSize, ConnectivityStatus.Offline, ct);

        public async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendsAsync(pageNum, pageSize, null, ct);

        public async UniTask<PaginatedFriendsResult> GetMutualFriendsAsync(string userId, int pageNum, int pageSize, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            var payload = new GetMutualFriendsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
                User = new User
                {
                    Address = userId,
                },
            };

            PaginatedUsersResponse response = await module!.CallUnaryProcedure<PaginatedUsersResponse>(GET_MUTUAL_FRIENDS_PROCEDURE_NAME, payload)
                                                           .AttachExternalCancellation(ct);

            allFriendsBuffer.Clear();

            foreach (var user in response.Users)
                allFriendsBuffer.Add(user.Address);

            IEnumerable<Profile> profiles = await FetchProfiles(allFriendsBuffer, ct);

            return new PaginatedFriendsResult(profiles, response.PaginationData.Total);
        }

        public async UniTask<FriendshipStatus> GetFriendshipStatusAsync(string userId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            var payload = new GetFriendshipStatusPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            GetFriendshipStatusResponse response = await module!.CallUnaryProcedure<GetFriendshipStatusResponse>(GET_FRIENDSHIP_STATUS_PROCEDURE_NAME, payload)
                                                                .AttachExternalCancellation(ct)
                                                                .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            switch (response.ResponseCase)
            {
                case GetFriendshipStatusResponse.ResponseOneofCase.InternalServerError:
                    throw new Exception($"Cannot fetch friendship status {response.ResponseCase}");
                case GetFriendshipStatusResponse.ResponseOneofCase.Accepted:
                    switch (response.Accepted.Status)
                    {
                        case Decentraland.SocialService.V3.FriendshipStatus.Accepted:
                            return FriendshipStatus.FRIEND;
                        case Decentraland.SocialService.V3.FriendshipStatus.Blocked:
                            return FriendshipStatus.BLOCKED;
                        case Decentraland.SocialService.V3.FriendshipStatus.RequestReceived:
                            return FriendshipStatus.REQUEST_RECEIVED;
                        case Decentraland.SocialService.V3.FriendshipStatus.RequestSent:
                            return FriendshipStatus.REQUEST_SENT;
                    }

                    break;
            }

            return FriendshipStatus.NONE;
        }

        public async UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendRequestsAsync(pageNum, pageSize, GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME, ct);

        public async UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendRequestsAsync(pageNum, pageSize, GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME, ct);

        public async UniTask RejectFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            await this.UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Request = new UpsertFriendshipPayload.Types.RequestPayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);
        }

        public async UniTask CancelFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            await this.UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Cancel = new UpsertFriendshipPayload.Types.CancelPayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);
        }

        public async UniTask AcceptFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            await this.UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Accept = new UpsertFriendshipPayload.Types.AcceptPayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);

            friendsCache.Add(friendId);
        }

        public async UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            await this.UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Delete = new UpsertFriendshipPayload.Types.DeletePayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);

            friendsCache.Remove(friendId);
        }

        public async UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            UpsertFriendshipResponse.Types.Accepted response = await UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Request = new UpsertFriendshipPayload.Types.RequestPayload
                {
                    Message = messageBody,
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);

            return new FriendRequest(GetFriendRequestId(friendId, response.CreatedAt),
                DateTimeOffset.FromUnixTimeSeconds(response.CreatedAt).DateTime,
                identityCache.EnsuredIdentity().Address,
                friendId,
                messageBody);
        }

        private async UniTask EnsureRpcConnectionAsync(CancellationToken ct)
        {
            switch (transport.State)
            {
                case WebSocketState.Open:
                    break;
                case WebSocketState.Connecting:
                    await UniTask.WaitWhile(() => transport.State == WebSocketState.Connecting, cancellationToken: ct);
                    break;
                default:
                    await transport.ConnectAsync(ct);
                    transport.ListenForIncomingData();

                    // The service expects the auth-chain in json format within a 30 seconds threshold after connection
                    await transport.SendMessageAsync(BuildAuthChain(), ct);
                    break;
            }

            if (module == null)
            {
                RpcClientPort port = await client.CreatePort("friends");
                module = await port.LoadModule(RPC_SERVICE_NAME);
            }

            return;

            string BuildAuthChain()
            {
                authChainBuffer.Clear();

                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                using AuthChain authChain = identityCache.EnsuredIdentity().Sign($"get:/:{timestamp}:{{}}");
                var authChainIndex = 0;

                foreach (AuthLink link in authChain)
                {
                    authChainBuffer[$"x-identity-auth-chain-{authChainIndex}"] = link.ToJson();
                    authChainIndex++;
                }

                authChainBuffer["x-identity-timestamp"] = timestamp.ToString();
                authChainBuffer["x-identity-metadata"] = "{}";

                return JsonConvert.SerializeObject(authChainBuffer);
            }
        }

        private async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, ConnectivityStatus? connectivityStatus, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            var payload = new GetFriendsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            if (connectivityStatus != null)
                payload.Status = connectivityStatus.Value;

            PaginatedUsersResponse response = await module!.CallUnaryProcedure<PaginatedUsersResponse>(GET_FRIENDS_PROCEDURE_NAME, payload)
                                                           .AttachExternalCancellation(ct);

            allFriendsBuffer.Clear();

            foreach (var user in response.Users)
            {
                allFriendsBuffer.Add(user.Address);
                friendsCache.Add(user.Address);
            }

            IEnumerable<Profile> profiles = await FetchProfiles(allFriendsBuffer, ct);

            return new PaginatedFriendsResult(profiles, response.PaginationData.Total);
        }

        private async UniTask<IEnumerable<Profile>> FetchProfiles(IEnumerable<string> ids, CancellationToken ct)
        {
            fetchProfileTasks.Clear();

            foreach (string userId in ids)
                fetchProfileTasks.Add(profileRepository.GetAsync(userId, ct));

            return (await UniTask.WhenAll(fetchProfileTasks))
               .Where(profile => profile != null)!;
        }

        private async UniTask<PaginatedFriendRequestsResult> GetFriendRequestsAsync(int pageNum, int pageSize, string procedureName,
            CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            friendRequestsBuffer.Clear();

            var payload = new GetFriendshipRequestsPayload
            {
                Pagination = new Pagination
                {
                    Offset = (pageNum - 1) * pageSize,
                    Limit = pageSize,
                },
            };

            PaginatedFriendshipRequestsResponse response = await module!.CallUnaryProcedure<PaginatedFriendshipRequestsResponse>(procedureName, payload)
                                                                        .AttachExternalCancellation(ct)
                                                                        .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            switch (response.ResponseCase)
            {
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (var rr in response.Requests.Requests)
                    {
                        var fr = new FriendRequest(
                            GetFriendRequestId(rr.User.Address, rr.CreatedAt),
                            DateTimeOffset.FromUnixTimeSeconds(rr.CreatedAt).DateTime,
                            rr.User.Address,
                            identityCache.EnsuredIdentity().Address,
                            rr.Message);

                        friendRequestsBuffer.Add(fr);
                    }

                    break;
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.InternalServerError:
                default:
                    throw new Exception($"Cannot fetch friend requests {procedureName} {response.ResponseCase}");
            }

            return new PaginatedFriendRequestsResult(friendRequestsBuffer, response.PaginationData.Total);
        }

        private async UniTask<UpsertFriendshipResponse.Types.Accepted> UpdateFriendshipAsync(
            UpsertFriendshipPayload payload,
            CancellationToken ct)
        {
            UpsertFriendshipResponse response = await module!.CallUnaryProcedure<UpsertFriendshipResponse>(UPDATE_FRIENDSHIP_PROCEDURE_NAME, payload)
                                                             .AttachExternalCancellation(ct)
                                                             .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            return response.ResponseCase switch
                   {
                       UpsertFriendshipResponse.ResponseOneofCase.Accepted => response.Accepted,
                       _ => throw new Exception($"Cannot update friendship {response.ResponseCase}")
                   };
        }

        // TODO: this method should be removed as the service has to return the request id
        private static string GetFriendRequestId(string userId, long createdAt) =>
            $"{userId}-{createdAt}";
    }
}
