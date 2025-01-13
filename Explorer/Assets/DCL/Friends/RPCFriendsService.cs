using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Decentraland.SocialServiceV2;
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
        private const string GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME = "GetSentFriendshipRequests";
        private const string GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME = "GetPendingFriendshipRequests";

        private readonly IFriendsEventBus eventBus;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
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
            IWeb3IdentityCache identityCache)
        {
            this.eventBus = eventBus;
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            transport = new WebSocketRpcTransport(new Uri(apiUrl));
            client = new RpcClient(transport);
        }

        public void Dispose()
        {
            transport.Dispose();
            client.Dispose();
        }

        public async UniTask SubscribeToIncomingFriendshipEvents(CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            IUniTaskAsyncEnumerable<FriendshipUpdate> stream =
                module!.CallServerStream<FriendshipUpdate>("SubscribeToFriendshipUpdates", new Empty());

            await foreach (var response in stream.WithCancellation(ct))
            {
                switch (response.UpdateCase)
                {
                    case FriendshipUpdate.UpdateOneofCase.Accept:
                        eventBus.BroadcastFriendRequestAccepted(response.Accept.User.Address);
                        break;

                    case FriendshipUpdate.UpdateOneofCase.Cancel:
                        eventBus.BroadcastFriendRequestCanceled(response.Cancel.User.Address);
                        break;

                    case FriendshipUpdate.UpdateOneofCase.Delete:
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

        public async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            allFriendsBuffer.Clear();
            // TODO: add support to pagination at server side
            await FetchAllFriendIdsAsync(allFriendsBuffer, ct);

            var ids = allFriendsBuffer.Skip((pageNum - 1) * pageSize)
                                       .Take(pageSize);

            fetchProfileTasks.Clear();

            foreach (string userId in ids)
                fetchProfileTasks.Add(profileRepository.GetAsync(userId, ct));

            var profiles = (await UniTask.WhenAll(fetchProfileTasks))
               .Where(profile => profile != null);

            return new PaginatedFriendsResult(profiles!, allFriendsBuffer.Count);
        }

        public async UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            allFriendsBuffer.Clear();
            // TODO: add support to this query at server side instead of fetching all the friends
            await FetchAllFriendIdsAsync(allFriendsBuffer, ct);

            return allFriendsBuffer.Contains(friendId);
        }

        public async UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendRequests(pageNum, pageSize, GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME, ct);

        public async UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct) =>
            await GetFriendRequests(pageNum, pageSize, GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME, ct);

        public async UniTask RejectFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            await this.UpdateFriendship(new UpsertFriendshipPayload
            {
                Request = new RequestPayload
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

            await this.UpdateFriendship(new UpsertFriendshipPayload
            {
                Cancel = new CancelPayload
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

            await this.UpdateFriendship(new UpsertFriendshipPayload
            {
                Accept = new AcceptPayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);
        }

        public async UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            await this.UpdateFriendship(new UpsertFriendshipPayload
            {
                Delete = new DeletePayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);
        }

        public async UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            // TODO: ideally the server should return the request information when it is created so we dont have to request it later
            await UpdateFriendship(new UpsertFriendshipPayload
            {
                Request = new RequestPayload
                {
                    Message = messageBody,
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);

            FriendRequest? fr = await GetSentFriendRequest(friendId, ct);

            if (fr == null)
                throw new Exception("Inconsistent friend request. Created but not found.");

            return fr;
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

        private async UniTask FetchAllFriendIdsAsync(HashSet<string> results, CancellationToken ct)
        {
            IUniTaskAsyncEnumerable<UsersResponse> stream = module!.CallServerStream<UsersResponse>("GetFriends", new Empty());

            await foreach (var response in stream.WithCancellation(ct))
            {
                if (response.Users == null) continue;

                foreach (var friend in response.Users)
                    results.Add(friend.Address);
            }
        }

        private async UniTask<PaginatedFriendRequestsResult> GetFriendRequests(int pageNum, int pageSize, string procedureName,
            CancellationToken ct)
        {
            await EnsureRpcConnectionAsync(ct);

            friendRequestsBuffer.Clear();

            FriendshipRequestsResponse response = await module!.CallUnaryProcedure<FriendshipRequestsResponse>(procedureName, new Empty())
                                                               .AttachExternalCancellation(ct)
                                                               .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            switch (response.ResponseCase)
            {
                case FriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (RequestResponse rr in response.Requests.Requests_)
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
                case FriendshipRequestsResponse.ResponseOneofCase.None:
                case FriendshipRequestsResponse.ResponseOneofCase.InternalServerError:
                default:
                    throw new Exception($"Cannot fetch sent friend requests {response.ResponseCase}");
            }

            IEnumerable<FriendRequest> requests = friendRequestsBuffer.Skip((pageNum - 1) * pageSize)
                                                                      .Take(pageSize);

            return new PaginatedFriendRequestsResult(requests, friendRequestsBuffer.Count);
        }

        private async UniTask<FriendRequest?> GetSentFriendRequest(string friendId, CancellationToken ct)
        {
            FriendshipRequestsResponse response = await module!.CallUnaryProcedure<FriendshipRequestsResponse>(GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME, new Empty())
                                                               .AttachExternalCancellation(ct)
                                                               .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            switch (response.ResponseCase)
            {
                case FriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (RequestResponse rr in response.Requests.Requests_)
                    {
                        if (rr.User.Address != friendId) continue;

                        return new FriendRequest(
                                GetFriendRequestId(rr.User.Address, rr.CreatedAt),
                                DateTimeOffset.FromUnixTimeSeconds(rr.CreatedAt).DateTime,
                                rr.User.Address,
                                identityCache.EnsuredIdentity().Address,
                                rr.Message);
                    }

                    break;
                case FriendshipRequestsResponse.ResponseOneofCase.None:
                case FriendshipRequestsResponse.ResponseOneofCase.InternalServerError:
                default:
                    throw new Exception($"Cannot fetch sent friend requests {response.ResponseCase}");
            }

            return null;
        }

        private async UniTask<UpsertFriendshipResponse.Types.Accepted> UpdateFriendship(
            UpsertFriendshipPayload payload,
            CancellationToken ct)
        {
            UpsertFriendshipResponse response = await module!.CallUnaryProcedure<UpsertFriendshipResponse>("UpsertFriendship", payload)
                                                             .AttachExternalCancellation(ct)
                                                             .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            return response.ResponseCase switch
                   {
                       UpsertFriendshipResponse.ResponseOneofCase.Accepted => response.Accepted,
                       _ => throw new Exception($"Cannot update friendship {response.ResponseCase}")
                   };
        }

        /// <summary>
        /// The service does not respond with the friend request id so we need to generate one at the fly
        /// </summary>
        private static string GetFriendRequestId(string userId, long createdAt) =>
            $"{userId}-{createdAt}";
    }
}
