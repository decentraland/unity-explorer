using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SocialService;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3;
using Decentraland.SocialService.V2;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Friends
{
    public class RPCFriendsService : IFriendsService
    {
        private const int TIMEOUT_SECONDS = 30;
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

        private const int RETRY_STREAM_THROTTLE_MS = 5000;

        private readonly IFriendsEventBus eventBus;
        private readonly FriendsCache friendsCache;
        private readonly ISelfProfile selfProfile;
        private readonly ObjectProxy<IRPCSocialServices> socialServiceRPCProxy;

        private readonly List<FriendRequest> receivedFriendRequestsBuffer = new ();
        private readonly List<FriendRequest> sentFriendRequestsBuffer = new ();
        private readonly List<FriendProfile> friendProfileBuffer = new ();
        private readonly List<BlockedProfile> blockedProfileBuffer = new ();

        private CancellationTokenSource subscriptionCancellationToken = new ();


        public RPCFriendsService(
            IFriendsEventBus eventBus,
            FriendsCache friendsCache,
            ISelfProfile selfProfile,
            ObjectProxy<IRPCSocialServices> socialServiceRPCProxy,
            ISocialServiceEventBus socialServiceEventBus)
        {
            this.eventBus = eventBus;
            this.friendsCache = friendsCache;
            this.selfProfile = selfProfile;
            this.socialServiceRPCProxy = socialServiceRPCProxy;
            socialServiceEventBus.TransportClosed += OnTransportClosed;
        }

        public async UniTask SubscribeToIncomingFriendshipEventsAsync(CancellationToken ct)
        {
            // We try to keep the stream open until cancellation is requested
            // If by any reason the rpc connection has a problem, we need to wait until it is restored, so we re-open the stream
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (socialServiceRPCProxy.Configured)
                    {
                        await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);
                        var subscriptionCt = CancellationTokenSource.CreateLinkedTokenSource(ct, subscriptionCancellationToken.Token);
                        await OpenStreamAndProcessUpdatesAsync().AttachExternalCancellation(subscriptionCt.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS)); }

                await UniTask.Delay(RETRY_STREAM_THROTTLE_MS, cancellationToken: ct);
            }

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<FriendshipUpdate> stream =
                    socialServiceRPCProxy.StrictObject.Module().CallServerStream<FriendshipUpdate>(SUBSCRIBE_FRIENDSHIP_UPDATES_PROCEDURE_NAME,
                        new Empty());

                // We could try stream.WithCancellation(ct) but the cancellation doesn't work.
                await foreach (var response in stream)
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
                                var request = response.Request;

                                Profile? myProfile = await selfProfile.ProfileAsync(ct);

                                var fr = new FriendRequest(
                                    request.Id,
                                    DateTimeOffset.FromUnixTimeMilliseconds(request.CreatedAt).DateTime,
                                    ToClientFriendProfile(request.Friend),
                                    ToClientFriendProfile(myProfile!),
                                    request.HasMessage ? request.Message : string.Empty);

                                eventBus.BroadcastFriendRequestReceived(fr);
                                break;
                        }
                    }

                    // Do exception handling as we need to keep the stream open in case we have an internal error in the processing of the data
                    catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS)); }
                }
            }
        }

        public async UniTask SubscribeToConnectivityStatusAsync(CancellationToken ct)
        {
            // We try to keep the stream open until cancellation is requested
            // If by any reason the rpc connection has a problem, we need to wait until it is restored, so we re-open the stream
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (socialServiceRPCProxy.Configured)
                    {
                        await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);
                        var subscriptionCt = CancellationTokenSource.CreateLinkedTokenSource(ct, subscriptionCancellationToken.Token);
                        await OpenStreamAndProcessUpdatesAsync().AttachExternalCancellation(subscriptionCt.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS)); }

                await UniTask.Delay(RETRY_STREAM_THROTTLE_MS, cancellationToken: ct);
            }

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<FriendConnectivityUpdate> stream =
                    socialServiceRPCProxy.StrictObject.Module()!.CallServerStream<FriendConnectivityUpdate>(SUBSCRIBE_TO_CONNECTIVITY_UPDATES, new Empty());

                // We could try stream.WithCancellation(ct) but the cancellation doesn't work.
                await foreach (var response in stream)
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

        public async UniTask SubscribeToUserBlockUpdatersAsync(CancellationToken ct)
        {
            // We try to keep the stream open until cancellation is requested
            // If by any reason the rpc connection has a problem, we need to wait until it is restored, so we re-open the stream
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (socialServiceRPCProxy.Configured)
                    {
                        await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);
                        var subscriptionCt = CancellationTokenSource.CreateLinkedTokenSource(ct, subscriptionCancellationToken.Token);
                        await OpenStreamAndProcessUpdatesAsync().AttachExternalCancellation(subscriptionCt.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS)); }

                await UniTask.Delay(RETRY_STREAM_THROTTLE_MS, cancellationToken: ct);
            }

            return;

            async UniTask OpenStreamAndProcessUpdatesAsync()
            {
                IUniTaskAsyncEnumerable<BlockUpdate> stream =
                    socialServiceRPCProxy.StrictObject.Module()!.CallServerStream<BlockUpdate>(SUBSCRIBE_TO_BLOCK_STATUS_UPDATES, new Empty());

                await foreach (var response in stream)
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

            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var payload = new GetBlockedUsersPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                       .CallUnaryProcedure<GetBlockedUsersResponse>(GET_BLOCKED_USERS, payload)
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            IEnumerable<BlockedProfile> profiles = ToClientBlockedProfiles(response.Profiles);

            return new PaginatedBlockedProfileResult(profiles, response.PaginationData.Total);
        }

        public async UniTask BlockUserAsync(string userId, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var payload = new BlockUserPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                       .CallUnaryProcedure<BlockUserResponse>(BLOCK_USER, payload)
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var payload = new UnblockUserPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                       .CallUnaryProcedure<UnblockUserResponse>(UNBLOCK_USER, payload)
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                       .CallUnaryProcedure<GetBlockingStatusResponse>(GET_BLOCKING_STATUS, new Empty())
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            return new UserBlockingStatus(response.BlockedUsers, response.BlockedByUsers);
        }

        public async UniTask<PaginatedFriendsResult> GetFriendsAsync(int pageNum, int pageSize, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var payload = new GetFriendsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                       .CallUnaryProcedure<PaginatedFriendsProfilesResponse>(GET_FRIENDS_PROCEDURE_NAME, payload)
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            foreach (var profile in response.Friends)
                friendsCache.Add(profile.Address);

            IEnumerable<FriendProfile> profiles = ToClientFriendProfiles(response.Friends);

            return new PaginatedFriendsResult(profiles, response.PaginationData.Total);
        }

        public async UniTask<PaginatedFriendsResult> GetMutualFriendsAsync(string userId, int pageNum, int pageSize,
            CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

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

            var response = await socialServiceRPCProxy.StrictObject.Module()!
                                                       .CallUnaryProcedure<PaginatedFriendsProfilesResponse>(GET_MUTUAL_FRIENDS_PROCEDURE_NAME, payload)
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            var profiles = ToClientFriendProfiles(response.Friends);

            return new PaginatedFriendsResult(profiles, response.PaginationData.Total);
        }

        public async UniTask<FriendshipStatus> GetFriendshipStatusAsync(string userId, CancellationToken ct)
        {
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            var payload = new GetFriendshipStatusPayload
            {
                User = new User
                {
                    Address = userId,
                },
            };

            GetFriendshipStatusResponse response = await socialServiceRPCProxy.StrictObject.Module()!
                                                                               .CallUnaryProcedure<GetFriendshipStatusResponse>(GET_FRIENDSHIP_STATUS_PROCEDURE_NAME, payload)
                                                                               .AttachExternalCancellation(ct)
                                                                               .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            receivedFriendRequestsBuffer.Clear();

            var payload = new GetFriendshipRequestsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            PaginatedFriendshipRequestsResponse response = await socialServiceRPCProxy.StrictObject.Module()!
                                                                                       .CallUnaryProcedure<PaginatedFriendshipRequestsResponse>(GET_RECEIVED_FRIEND_REQUESTS_PROCEDURE_NAME,
                                                                                            payload)
                                                                                       .AttachExternalCancellation(ct)
                                                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            Profile? myProfile = await selfProfile.ProfileAsync(ct);

            switch (response.ResponseCase)
            {
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (var rr in response.Requests.Requests)
                    {
                        var fr = new FriendRequest(
                            rr.Id,
                            DateTimeOffset.FromUnixTimeMilliseconds(rr.CreatedAt).DateTime,
                            ToClientFriendProfile(rr.Friend),
                            ToClientFriendProfile(myProfile!),
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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

            sentFriendRequestsBuffer.Clear();

            var payload = new GetFriendshipRequestsPayload
            {
                Pagination = new Pagination
                {
                    Offset = pageNum * pageSize,
                    Limit = pageSize,
                },
            };

            PaginatedFriendshipRequestsResponse response = await socialServiceRPCProxy.StrictObject.Module()!
                                                                                       .CallUnaryProcedure<PaginatedFriendshipRequestsResponse>(GET_SENT_FRIEND_REQUESTS_PROCEDURE_NAME,
                                                                                            payload)
                                                                                       .AttachExternalCancellation(ct)
                                                                                       .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            Profile? myProfile = await selfProfile.ProfileAsync(ct);

            switch (response.ResponseCase)
            {
                case PaginatedFriendshipRequestsResponse.ResponseOneofCase.Requests:
                    foreach (var rr in response.Requests.Requests)
                    {
                        var fr = new FriendRequest(
                            rr.Id,
                            DateTimeOffset.FromUnixTimeMilliseconds(rr.CreatedAt).DateTime,
                            ToClientFriendProfile(myProfile!),
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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

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
            await socialServiceRPCProxy.StrictObject.EnsureRpcConnectionAsync(ct);

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
                ToClientFriendProfile(myProfile!),
                ToClientFriendProfile(response.Friend),
                messageBody);

            eventBus.BroadcastThatYouSentFriendRequestToOtherUser(fr);

            return fr;
        }

        private async UniTask<UpsertFriendshipResponse.Types.Accepted> UpdateFriendshipAsync(
            UpsertFriendshipPayload payload,
            CancellationToken ct)
        {
            UpsertFriendshipResponse response = await socialServiceRPCProxy.StrictObject.Module()!
                                                                            .CallUnaryProcedure<UpsertFriendshipResponse>(UPDATE_FRIENDSHIP_PROCEDURE_NAME, payload)
                                                                            .AttachExternalCancellation(ct)
                                                                            .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

            return response.ResponseCase switch
                   {
                       UpsertFriendshipResponse.ResponseOneofCase.Accepted => response.Accepted,
                       _ => throw new Exception($"Cannot update friendship {response.ResponseCase}")
                   };
        }

        private IEnumerable<FriendProfile> ToClientFriendProfiles(
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

        private FriendProfile ToClientFriendProfile(Decentraland.SocialService.V2.FriendProfile profile)
        {
            var fp = new FriendProfile(new Web3Address(profile.Address),
                profile.Name,
                profile.HasClaimedName,
                URLAddress.FromString(profile.ProfilePictureUrl),
                ProfileNameColorHelper.GetNameColor(profile.Name));

            return fp;
        }

        private BlockedProfile ToClientBlockedProfile(BlockedUserProfile profile)
        {
            var fp = new BlockedProfile(new Web3Address(profile.Address),
                profile.Name,
                profile.HasClaimedName,
                URLAddress.FromString(profile.ProfilePictureUrl),
                DateTimeOffset.FromUnixTimeMilliseconds(profile.BlockedAt).DateTime,
                ProfileNameColorHelper.GetNameColor(profile.Name));

            return fp;
        }

        private FriendProfile ToClientFriendProfile(Profile profile)
        {
            var fp = new FriendProfile(new Web3Address(profile.UserId),
                profile.Name,
                profile.HasClaimedName,
                profile.Avatar.FaceSnapshotUrl,
                ProfileNameColorHelper.GetNameColor(profile.Name));

            return fp;
        }

        private void OnTransportClosed() =>
            subscriptionCancellationToken = subscriptionCancellationToken.SafeRestart();

    }
}
