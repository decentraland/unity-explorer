using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SocialService;
using DCL.Utilities;
using DCL.Web3;
using Decentraland.SocialService.V2;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends
{
    public class RPCFriendsService : RPCSocialServiceBase, IFriendsService
    {
        /// <summary>
        ///     Timeout used for foreground operations, such as fetching the list of friends
        /// </summary>
        private const int FOREGROUND_TIMEOUT_SECONDS = 30;

        private const string GET_FRIENDS_PROCEDURE_NAME = "GetFriends";
        private const string GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME = "GetSentFriendshipRequests";
        private const string GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME = "GetPendingFriendshipRequests";
        private const string GET_FRIENDSHIP_STATUS_PROCEDURE_NAME = "GetFriendshipStatus";
        private const string UPDATE_FRIENDSHIP_PROCEDURE_NAME = "UpsertFriendship";
        private const string SUBSCRIBE_FRIENDSHIP_UPDATES_PROCEDURE_NAME = "SubscribeToFriendshipUpdates";
        private const string GET_MUTUAL_FRIENDS_PROCEDURE_NAME = "GetMutualFriends";
        private const string SUBSCRIBE_TO_CONNECTIVITY_UPDATES = "SubscribeToFriendConnectivityUpdates";
        private const string SUBSCRIBE_TO_BLOCK_STATUS_UPDATES = "SubscribeToBlockUpdates";
        private const string GET_BLOCKED_USERS = "GetBlockedUsers";
        private const string GET_BLOCKING_STATUS = "GetBlockingStatus";
        private const string BLOCK_USER = "BlockUser";
        private const string UNBLOCK_USER = "UnblockUser";

        private readonly IFriendsEventBus eventBus;
        private readonly FriendsCache friendsCache;
        private readonly ISelfProfile selfProfile;

        private readonly List<FriendRequest> receivedFriendRequestsBuffer = new ();
        private readonly List<FriendRequest> sentFriendRequestsBuffer = new ();
        private readonly List<Profile.CompactInfo> friendProfileBuffer = new ();
        private readonly List<BlockedProfile> blockedProfileBuffer = new ();

        public RPCFriendsService(
            IFriendsEventBus eventBus,
            FriendsCache friendsCache,
            ISelfProfile selfProfile,
            IRPCSocialServices socialServiceRPC) : base(socialServiceRPC, ReportCategory.FRIENDS)
        {
            this.eventBus = eventBus;
            this.friendsCache = friendsCache;
            this.selfProfile = selfProfile;
        }

        public UniTask SubscribeToIncomingFriendshipEventsAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<FriendshipUpdate> stream =
                    socialServiceRPC.Module()
                                    .CallServerStream<FriendshipUpdate>(SUBSCRIBE_FRIENDSHIP_UPDATES_PROCEDURE_NAME,
                                         new Empty());

                await foreach (FriendshipUpdate? response in stream)
                {
                    try
                    {
                        switch (response.UpdateCase)
                        {
                            case FriendshipUpdate.UpdateOneofCase.Accept:
                                friendsCache.Add(response.Accept.User.Address);
                                eventBus.BroadcastThatOtherUserAcceptedYourRequest(response.Accept.User.Address);
                                break;

                            case FriendshipUpdate.UpdateOneofCase.Cancel:
                                eventBus.BroadcastThatOtherUserCancelledTheRequest(response.Cancel.User.Address);
                                break;

                            case FriendshipUpdate.UpdateOneofCase.Delete:
                                friendsCache.Remove(response.Delete.User.Address);
                                eventBus.BroadcastThatOtherUserRemovedTheFriendship(response.Delete.User.Address);
                                break;

                            case FriendshipUpdate.UpdateOneofCase.Reject:
                                eventBus.BroadcastThatOtherUserRejectedYourRequest(response.Reject.User.Address);
                                break;

                            case FriendshipUpdate.UpdateOneofCase.Request:
                                FriendshipUpdate.Types.RequestResponse? request = response.Request;

                                Profile? myProfile = await selfProfile.ProfileAsync(ct);

                                var fr = new FriendRequest(
                                    request.Id,
                                    DateTimeOffset.FromUnixTimeMilliseconds(request.CreatedAt).DateTime,
                                    ToClientFriendProfile(request.Friend),
                                    myProfile!.Compact,
                                    request.HasMessage ? request.Message : string.Empty);

                                eventBus.BroadcastFriendRequestReceived(fr);
                                break;
                        }
                    }

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    // No need to handle OperationCancelledException because there are no async calls
                    catch (Exception e) { ReportHub.LogException(e, ReportCategory.FRIENDS); }
                }
            }
        }

        public UniTask SubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<FriendConnectivityUpdate> stream =
                    socialServiceRPC.Module()!.CallServerStream<FriendConnectivityUpdate>(SUBSCRIBE_TO_CONNECTIVITY_UPDATES, new Empty());

                // We could try stream.WithCancellation(ct) but the cancellation doesn't work.
                await foreach (FriendConnectivityUpdate? response in stream)
                {
                    try
                    {
                        switch (response.Status)
                        {
                            case ConnectivityStatus.Away:
                                eventBus.BroadcastFriendAsAway(ToClientFriendProfile(response.Friend));
                                break;
                            case ConnectivityStatus.Offline:
                                eventBus.BroadcastFriendDisconnected(ToClientFriendProfile(response.Friend));
                                break;
                            case ConnectivityStatus.Online:
                                eventBus.BroadcastFriendConnected(ToClientFriendProfile(response.Friend));
                                break;
                        }
                    }

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS)); }
                }
            }
        }

        public UniTask SubscribeToUserBlockUpdatersAsync(CancellationToken ct)
        {
            return KeepServerStreamOpenAsync(OpenStreamAndProcessUpdatesAsync, ct);

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<BlockUpdate> stream =
                    socialServiceRPC.Module()!.CallServerStream<BlockUpdate>(SUBSCRIBE_TO_BLOCK_STATUS_UPDATES, new Empty());

                await foreach (BlockUpdate? response in stream)
                {
                    try
                    {
                        if (response.IsBlocked)
                            eventBus.BroadcastOtherUserBlockedYou(response.Address);
                        else
                            eventBus.BroadcastOtherUserUnblockedYou(response.Address);
                    }

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS)); }
                }
            }
        }

        public async UniTask<PaginatedBlockedProfileResult> GetBlockedUsersAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new GetBlockedUsersPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            GetBlockedUsersResponse? response = await socialServiceRPC.Module()!
                                                                      .CallUnaryProcedure<GetBlockedUsersResponse>(GET_BLOCKED_USERS, payload)
                                                                      .AttachExternalCancellation(ct)
                                                                      .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            IEnumerable<BlockedProfile> profiles = ToClientBlockedProfiles(response.Profiles);

            return new PaginatedBlockedProfileResult(profiles, response.PaginationData.Total);
        }

        public async UniTask BlockUserAsync(string userId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new BlockUserPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            BlockUserResponse? response = await socialServiceRPC.Module()!
                                                                .CallUnaryProcedure<BlockUserResponse>(BLOCK_USER, payload)
                                                                .AttachExternalCancellation(ct)
                                                                .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            if (response.ResponseCase == BlockUserResponse.ResponseOneofCase.Ok)
            {
                eventBus.BroadcastYouBlockedProfile(ToClientBlockedProfile(response.Ok.Profile));
                friendsCache.Remove(userId);
            }
            else
                throw new Exception($"Cannot block user {userId}: {response.ResponseCase}");
        }

        public async UniTask UnblockUserAsync(string userId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new UnblockUserPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            UnblockUserResponse? response = await socialServiceRPC.Module()!
                                                                  .CallUnaryProcedure<UnblockUserResponse>(UNBLOCK_USER, payload)
                                                                  .AttachExternalCancellation(ct)
                                                                  .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            if (response.ResponseCase == UnblockUserResponse.ResponseOneofCase.Ok)
            {
                BlockedProfile blockedProfile = ToClientBlockedProfile(response.Ok.Profile);
                eventBus.BroadcastYouUnblockedProfile(blockedProfile);
            }
            else
                throw new Exception($"Cannot unblock user {userId}: {response.ResponseCase}");
        }

        public async UniTask<UserBlockingStatus> GetUserBlockingStatusAsync(CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            GetBlockingStatusResponse? response = await socialServiceRPC.Module()!
                                                                        .CallUnaryProcedure<GetBlockingStatusResponse>(GET_BLOCKING_STATUS, new Empty())
                                                                        .AttachExternalCancellation(ct)
                                                                        .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return new UserBlockingStatus(response.BlockedUsers, response.BlockedByUsers);
        }

        public async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new GetFriendsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            PaginatedFriendsProfilesResponse? response = await socialServiceRPC.Module()!
                                                                               .CallUnaryProcedure<PaginatedFriendsProfilesResponse>(GET_FRIENDS_PROCEDURE_NAME, payload)
                                                                               .AttachExternalCancellation(ct)
                                                                               .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            foreach (Decentraland.SocialService.V2.FriendProfile? profile in response.Friends)
                friendsCache.Add(profile.Address);

            IEnumerable<Profile.CompactInfo> profiles = ToClientFriendProfiles(response.Friends);

            return new PaginatedFriendsResult(profiles, response.PaginationData.Total);
        }

        public async UniTask<PaginatedFriendsResult> GetMutualFriendsAsync(string userId, int pageNum, int pageSize,
            CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

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

            PaginatedFriendsProfilesResponse? response = await socialServiceRPC.Module()!
                                                                               .CallUnaryProcedure<PaginatedFriendsProfilesResponse>(GET_MUTUAL_FRIENDS_PROCEDURE_NAME, payload)
                                                                               .AttachExternalCancellation(ct)
                                                                               .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            IEnumerable<Profile.CompactInfo> profiles = ToClientFriendProfiles(response.Friends);

            return new PaginatedFriendsResult(profiles, response.PaginationData.Total);
        }

        public async UniTask<FriendshipStatus> GetFriendshipStatusAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("GetFriendshipStatus called with empty userId", nameof(userId));

            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            var payload = new GetFriendshipStatusPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            GetFriendshipStatusResponse response = await socialServiceRPC.Module()!
                                                                         .CallUnaryProcedure<GetFriendshipStatusResponse>(GET_FRIENDSHIP_STATUS_PROCEDURE_NAME, payload)
                                                                         .AttachExternalCancellation(ct)
                                                                         .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            switch (response.ResponseCase)
            {
                case GetFriendshipStatusResponse.ResponseOneofCase.InternalServerError:
                    throw new Exception(
                        $"Cannot fetch friendship status {response.ResponseCase}: {response.InternalServerError}");
                case GetFriendshipStatusResponse.ResponseOneofCase.Accepted:
                    switch (response.Accepted.Status)
                    {
                        case Decentraland.SocialService.V2.FriendshipStatus.Accepted:
                            return FriendshipStatus.FRIEND;
                        case Decentraland.SocialService.V2.FriendshipStatus.Blocked:
                            return FriendshipStatus.BLOCKED;
                        case Decentraland.SocialService.V2.FriendshipStatus.RequestReceived:
                            return FriendshipStatus.REQUEST_RECEIVED;
                        case Decentraland.SocialService.V2.FriendshipStatus.RequestSent:
                            return FriendshipStatus.REQUEST_SENT;
                        case Decentraland.SocialService.V2.FriendshipStatus.BlockedBy:
                            return FriendshipStatus.BLOCKED_BY;
                    }

                    break;
            }

            return FriendshipStatus.NONE;
        }

        public async UniTask<PaginatedFriendRequestsResult> GetReceivedFriendRequestsAsync(int pageNum, int pageSize,
            CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            receivedFriendRequestsBuffer.Clear();

            var payload = new GetFriendshipRequestsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            PaginatedFriendshipRequestsResponse response = await socialServiceRPC.Module()!
                                                                                 .CallUnaryProcedure<PaginatedFriendshipRequestsResponse>(GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME,
                                                                                      payload)
                                                                                 .AttachExternalCancellation(ct)
                                                                                 .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            Profile? myProfile = await selfProfile.ProfileAsync(ct);

            switch (response.ResponseCase)
            {
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (FriendshipRequestResponse? rr in response.Requests.Requests)
                    {
                        var fr = new FriendRequest(
                            rr.Id,
                            DateTimeOffset.FromUnixTimeMilliseconds(rr.CreatedAt).DateTime,
                            ToClientFriendProfile(rr.Friend),
                            myProfile!.Compact,
                            rr.Message);

                        receivedFriendRequestsBuffer.Add(fr);
                    }

                    break;
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.InternalServerError:
                default:
                    throw new Exception($"Cannot fetch received friend requests {response.ResponseCase}");
            }

            return new PaginatedFriendRequestsResult(receivedFriendRequestsBuffer, response.PaginationData?.Total ?? 0);
        }

        public async UniTask<PaginatedFriendRequestsResult> GetSentFriendRequestsAsync(int pageNum, int pageSize,
            CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            sentFriendRequestsBuffer.Clear();

            var payload = new GetFriendshipRequestsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            PaginatedFriendshipRequestsResponse response = await socialServiceRPC.Module()!
                                                                                 .CallUnaryProcedure<PaginatedFriendshipRequestsResponse>(GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME,
                                                                                      payload)
                                                                                 .AttachExternalCancellation(ct)
                                                                                 .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            Profile? myProfile = await selfProfile.ProfileAsync(ct);

            switch (response.ResponseCase)
            {
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (FriendshipRequestResponse? rr in response.Requests.Requests)
                    {
                        var fr = new FriendRequest(
                            rr.Id,
                            DateTimeOffset.FromUnixTimeMilliseconds(rr.CreatedAt).DateTime,
                            myProfile!.Compact,
                            ToClientFriendProfile(rr.Friend),
                            rr.Message);

                        sentFriendRequestsBuffer.Add(fr);
                    }

                    break;
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.InternalServerError:
                default:
                    throw new Exception($"Cannot fetch received friend requests {response.ResponseCase}");
            }

            return new PaginatedFriendRequestsResult(sentFriendRequestsBuffer, response.PaginationData?.Total ?? 0);
        }

        public async UniTask RejectFriendshipAsync(string friendId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            await UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Reject = new UpsertFriendshipPayload.Types.RejectPayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);

            eventBus.BroadcastThatYouRejectedFriendRequestReceivedFromOtherUser(friendId);
        }

        public async UniTask CancelFriendshipAsync(string friendId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            await UpdateFriendshipAsync(new UpsertFriendshipPayload
            {
                Cancel = new UpsertFriendshipPayload.Types.CancelPayload
                {
                    User = new User
                    {
                        Address = friendId,
                    },
                },
            }, ct);

            eventBus.BroadcastThatYouCancelledFriendRequestSentToOtherUser(friendId);
        }

        public async UniTask AcceptFriendshipAsync(string friendId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            await UpdateFriendshipAsync(new UpsertFriendshipPayload
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

            eventBus.BroadcastThatYouAcceptedFriendRequestReceivedFromOtherUser(friendId);
        }

        public async UniTask DeleteFriendshipAsync(string friendId, CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

            await UpdateFriendshipAsync(new UpsertFriendshipPayload
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

            eventBus.BroadcastThatYouRemovedFriend(friendId);
        }

        public async UniTask<FriendRequest> RequestFriendshipAsync(string friendId, string messageBody,
            CancellationToken ct)
        {
            await socialServiceRPC.EnsureRpcConnectionAsync(ct);

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

            Profile? myProfile = await selfProfile.ProfileAsync(ct);

            var fr = new FriendRequest(response.Id,
                DateTimeOffset.FromUnixTimeMilliseconds(response.CreatedAt).DateTime,
                myProfile!.Compact,
                ToClientFriendProfile(response.Friend),
                messageBody);

            eventBus.BroadcastThatYouSentFriendRequestToOtherUser(fr);

            return fr;
        }

        private async UniTask<UpsertFriendshipResponse.Types.Accepted> UpdateFriendshipAsync(
            UpsertFriendshipPayload payload,
            CancellationToken ct)
        {
            UpsertFriendshipResponse response = await socialServiceRPC.Module()!
                                                                      .CallUnaryProcedure<UpsertFriendshipResponse>(UPDATE_FRIENDSHIP_PROCEDURE_NAME, payload)
                                                                      .AttachExternalCancellation(ct)
                                                                      .Timeout(TimeSpan.FromSeconds(FOREGROUND_TIMEOUT_SECONDS));

            return response.ResponseCase switch
                   {
                       UpsertFriendshipResponse.ResponseOneofCase.Accepted => response.Accepted,
                       _ => throw new Exception($"Cannot update friendship {response.ResponseCase}"),
                   };
        }

        private IReadOnlyList<Profile.CompactInfo> ToClientFriendProfiles(
            RepeatedField<Decentraland.SocialService.V2.FriendProfile> friends)
        {
            friendProfileBuffer.Clear();

            foreach (Decentraland.SocialService.V2.FriendProfile profile in friends)
                friendProfileBuffer.Add(ToClientFriendProfile(profile));

            return friendProfileBuffer;
        }

        private IEnumerable<BlockedProfile> ToClientBlockedProfiles(
            RepeatedField<BlockedUserProfile> friends)
        {
            blockedProfileBuffer.Clear();

            foreach (BlockedUserProfile profile in friends)
                blockedProfileBuffer.Add(ToClientBlockedProfile(profile));

            return blockedProfileBuffer;
        }

        [Obsolete(IProfileRepository.PROFILE_FRAGMENTATION_OBSOLESCENCE)]
        private Profile.CompactInfo ToClientFriendProfile(Decentraland.SocialService.V2.FriendProfile profile) =>
            new (profile.Address, profile.Name, profile.HasClaimedName, profile.ProfilePictureUrl);

        private BlockedProfile ToClientBlockedProfile(BlockedUserProfile profile)
        {
            var fp = new BlockedProfile(new Web3Address(profile.Address),
                profile.Name,
                profile.HasClaimedName,
                profile.ProfilePictureUrl,
                DateTimeOffset.FromUnixTimeMilliseconds(profile.BlockedAt).DateTime);

            return fp;
        }
    }
}
