using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
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
        private const int TIMEOUT_SECONDS = 30;

        private readonly IFriendsEventBus eventBus;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly WebSocketRpcTransport transport;
        private readonly RpcClient client;
        private readonly List<UniTask<Profile?>> fetchProfileTasks = new ();
        private readonly List<FriendRequest> friendRequestsBuffer = new ();
        private HashSet<string>? allFriendIds;
        private ClientFriendshipsService? service;

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

        public async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            var ids = allFriendIds!.Skip((pageNum - 1) * pageSize)
                                   .Take(pageSize);

            fetchProfileTasks.Clear();

            foreach (string userId in ids)
                fetchProfileTasks.Add(profileRepository.GetAsync(userId, ct));

            var profiles = (await UniTask.WhenAll(fetchProfileTasks))
               .Where(profile => profile != null);

            return new PaginatedFriendsResult(profiles!, allFriendIds!.Count);
        }

        public async UniTask<bool> IsFriendAsync(string friendId, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            return allFriendIds!.Contains(friendId);
        }

        public async UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            friendRequestsBuffer.Clear();

            var response = await service!.GetRequestEvents(new Payload
            {
                // TODO
                SynapseToken = "",
            }).AttachExternalCancellation(ct);

            switch (response.ResponseCase)
            {
                case RequestEventsResponse.ResponseOneofCase.Events:
                    foreach (RequestResponse friendRequest in response.Events.Incoming.Items)
                    {
                        friendRequestsBuffer.Add(new FriendRequest(
                            GetFriendRequestId(friendRequest.User.Address, friendRequest.CreatedAt),
                            DateTimeOffset.FromUnixTimeSeconds(friendRequest.CreatedAt).DateTime,
                            friendRequest.User.Address,
                            identityCache.Identity!.Address,
                            friendRequest.Message));
                    }
                    break;
                default:
                    throw new Exception($"Cannot fetch sent friend requests {response.ResponseCase}");
            }

            IEnumerable<FriendRequest> requests = friendRequestsBuffer.Skip((pageNum - 1) * pageSize)
                                                                      .Take(pageSize);

            return new PaginatedFriendRequestsResult(requests, friendRequestsBuffer.Count);
        }

        public async UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            friendRequestsBuffer.Clear();

            var response = await service!.GetRequestEvents(new Payload
            {
                // TODO
                SynapseToken = "",
            }).AttachExternalCancellation(ct);

            switch (response.ResponseCase)
            {
                case RequestEventsResponse.ResponseOneofCase.Events:
                    foreach (RequestResponse friendRequest in response.Events.Outgoing.Items)
                    {
                        friendRequestsBuffer.Add(new FriendRequest(
                            GetFriendRequestId(friendRequest.User.Address, friendRequest.CreatedAt),
                            DateTimeOffset.FromUnixTimeSeconds(friendRequest.CreatedAt).DateTime,
                            identityCache.Identity!.Address,
                            friendRequest.User.Address,
                            friendRequest.Message));
                    }
                    break;
                default:
                    throw new Exception($"Cannot fetch sent friend requests {response.ResponseCase}");
            }

            IEnumerable<FriendRequest> requests = friendRequestsBuffer.Skip((pageNum - 1) * pageSize)
                                                                      .Take(pageSize);

            return new PaginatedFriendRequestsResult(requests, friendRequestsBuffer.Count);
        }

        public async UniTask RejectFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            var payload = new UpdateFriendshipPayload
            {
                Event = new FriendshipEventPayload
                {
                    Reject = new RejectPayload
                    {
                        User = new User { Address = friendId },
                    },
                },
                AuthToken = new Payload
                {
                    // TODO:
                    SynapseToken = "",
                },
            };

            await this.UpdateFriendship(payload, ct);
        }

        public async UniTask CancelFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            var payload = new UpdateFriendshipPayload
            {
                Event = new FriendshipEventPayload
                {
                    Cancel = new CancelPayload
                    {
                        User = new User { Address = friendId },
                    },
                },
                AuthToken = new Payload
                {
                    // TODO
                    SynapseToken = "",
                },
            };

            await this.UpdateFriendship(payload, ct);
        }

        public async UniTask AcceptFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            var payload = new UpdateFriendshipPayload
            {
                Event = new FriendshipEventPayload
                {
                    Accept = new AcceptPayload
                    {
                        User = new User { Address = friendId },
                    },
                },
                AuthToken = new Payload
                {
                    // TODO
                    SynapseToken = "",
                },
            };

            await this.UpdateFriendship(payload, ct);

            allFriendIds!.Add(friendId);
        }

        public async UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            var payload = new UpdateFriendshipPayload
            {
                Event = new FriendshipEventPayload
                {
                    Delete = new DeletePayload
                    {
                        User = new User { Address = friendId },
                    },
                },
                AuthToken = new Payload
                {
                    // TODO
                    SynapseToken = "",
                },
            };

            await this.UpdateFriendship(payload, ct);

            allFriendIds!.Remove(friendId);
        }

        public async UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody, CancellationToken ct)
        {
            await EnsureEssentialDataToBeInitialized(ct);

            var payload = new UpdateFriendshipPayload
            {
                Event = new FriendshipEventPayload
                {
                    Request = new RequestPayload
                    {
                        Message = messageBody,
                        User = new User { Address = friendId },
                    },
                },
                AuthToken = new Payload
                {
                    // TODO
                    SynapseToken = "",
                },
            };

            FriendshipEventResponse @event = await UpdateFriendship(payload, ct);
            RequestResponse response = @event.Request;

            return new FriendRequest(
                GetFriendRequestId(response.User.Address, response.CreatedAt),
                DateTimeOffset.FromUnixTimeSeconds(response.CreatedAt).DateTime,
                identityCache.Identity!.Address,
                response.User.Address,
                response.HasMessage ? response.Message : string.Empty);
        }

        private async UniTask EnsureEssentialDataToBeInitialized(CancellationToken ct)
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

            await foreach (var response in stream.WithCancellation(ct))
            {
                switch (response.ResponseCase)
                {
                    case UsersResponse.ResponseOneofCase.Users:
                        if (response.Users != null)
                            foreach (User friend in response.Users.Users_)
                                results.Add(friend.Address);

                        break;

                    default:
                        throw new Exception($"Cannot fetch friend list {response.ResponseCase}");
                }
            }
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
                    break;
            }

            if (service == null)
            {
                RpcClientPort port = await client.CreatePort("friends");
                RpcClientModule module = await port.LoadModule(FriendshipsServiceCodeGen.ServiceName);
                service = new ClientFriendshipsService(module);
            }
        }

        private async UniTask<FriendshipEventResponse> UpdateFriendship(
            UpdateFriendshipPayload updateFriendshipPayload,
            CancellationToken ct)
        {
            UpdateFriendshipResponse? response = await service!.UpdateFriendshipEvent(updateFriendshipPayload)
                                                               .AttachExternalCancellation(ct)
                                                               .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            ct.ThrowIfCancellationRequested();

            switch (response.ResponseCase)
            {
                case UpdateFriendshipResponse.ResponseOneofCase.Event:
                    return response.Event;
                case UpdateFriendshipResponse.ResponseOneofCase.InternalServerError:
                case UpdateFriendshipResponse.ResponseOneofCase.UnauthorizedError:
                case UpdateFriendshipResponse.ResponseOneofCase.ForbiddenError:
                case UpdateFriendshipResponse.ResponseOneofCase.TooManyRequestsError:
                case UpdateFriendshipResponse.ResponseOneofCase.BadRequestError:
                default:
                    throw new Exception($"Cannot update friendship {response.ResponseCase}");
            }
        }

        /// <summary>
        /// The service does not respond with the friend request id so we need to generate one at the fly
        /// </summary>
        private static string GetFriendRequestId(string userId, long createdAt) =>
            $"{userId}-{createdAt}";
    }
}
