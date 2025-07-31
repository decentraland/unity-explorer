using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Utilities;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Multithreading;
using Utility.Types;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    /// Manages and provides data about members in the current chat channel. This service has two primary functions:
    /// 1. Provide a continuous, lightweight member count for UI elements like the chat title bar.
    /// 2. Provide a full, detailed list of members on-demand for the member list panel, with efficient live updates.
    /// </summary>
    public class ChatMemberListService : IDisposable
    {
        private const int UNIFIED_POLL_INTERVAL_MS = 500;

        private readonly CurrentChannelService currentChannelService;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;

        private readonly List<ChatMemberListView.MemberData> membersBuffer = new();

        private readonly CancellationTokenSource lifeCts = new();
        private CancellationTokenSource? channelCts;
        private CancellationTokenSource? liveListUpdateCts;
        private CancellationTokenSource? communityTaskCts;
        private readonly HashSet<string> lastKnownMemberIds = new ();

        private int lastKnownParticipantCount = -1;
        private int lastKnownMemberCount = -1;

        /// <summary>
        ///     Fires when the total number of members in the current channel changes.
        ///     This is a lightweight event designed for the title bar's member counter.
        /// </summary>
        public event Action<int>? OnMemberCountUpdated;

        /// <summary>
        ///     Fires with a detailed list of members after an update is triggered.
        ///     This is used to populate the full member list view.
        /// </summary>
        public event Action<IReadOnlyList<ChatMemberListView.MemberData>>? OnMemberListUpdated;

        public ChatMemberListService(IRoomHub roomHub,
            IProfileCache profileCache,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            CurrentChannelService currentChannelService,
            CommunitiesDataProvider communitiesDataProvider,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.roomHub = roomHub;
            this.profileCache = profileCache;
            this.friendsServiceProxy = friendsServiceProxy;
            this.currentChannelService = currentChannelService;
            this.communitiesDataProvider = communitiesDataProvider;
            this.web3IdentityCache = web3IdentityCache;
        }

        /// <summary>
        ///     Starts the service by subscribing to channel changes.
        /// </summary>
        public void Start()
        {
            if (!friendsServiceProxy.Configured)
            {
                ReportHub.LogWarning(ReportCategory.UI, "[ChatMemberListService] FriendsService is not configured. Cannot start member list service.");
                return;
            }

            currentChannelService.OnChannelChanged += OnChannelChanged;
            OnChannelChanged(currentChannelService.CurrentChannel);
        }

        /// <summary>
        ///     Stops the service, cancels all running tasks, and unsubscribes from events.
        /// </summary>
        private void Stop()
        {
            StopLiveMemberUpdates();
            TearDownCurrentChannelListeners();
            currentChannelService.OnChannelChanged -= OnChannelChanged;
            lifeCts.SafeCancelAndDispose();
        }

        /// <summary>
        ///     Performs a single, fresh fetch of the full member list.
        ///     This should be called by the UI when the member list panel is first opened.
        /// </summary>
        public async UniTask RequestInitialMemberListAsync()
        {
            if (channelCts == null || channelCts.IsCancellationRequested) return;
            await RefreshFullListAsync(channelCts.Token);
        }

        /// <summary>
        ///     Starts a background polling loop to check for changes in the member list.
        ///     The polling strategy is optimized based on the current channel type.
        ///     This should be called AFTER the initial list is displayed.
        /// </summary>
        public void StartLiveMemberUpdates()
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Starting live member updates...");
            StopLiveMemberUpdates();
            liveListUpdateCts = CancellationTokenSource.CreateLinkedTokenSource(lifeCts.Token);
            LiveMemberUpdateLoopAsync(liveListUpdateCts.Token).Forget();
        }

        /// <summary>
        ///     Stops the live member list polling.
        ///     This should be called when the member list panel is closed to conserve resources.
        /// </summary>
        public void StopLiveMemberUpdates()
        {
            ReportHub.Log(ReportCategory.UI, "[ChatMemberListService] Stopping live member updates...");
            liveListUpdateCts?.Cancel();
            liveListUpdateCts?.Dispose();
            liveListUpdateCts = null;
        }

        /// <summary>
        ///     Reacts to changes in the current chat channel by tearing down old listeners
        ///     and setting up new ones appropriate for the new channel type.
        /// </summary>
        private void OnChannelChanged(ChatChannel? newChannel)
        {
            TearDownCurrentChannelListeners();
            channelCts = CancellationTokenSource.CreateLinkedTokenSource(lifeCts.Token);

            if (newChannel == null)
            {
                UpdateAndBroadcastCount(0);
                return;
            }

            switch (newChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    SetupNearbyListeners();
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    StartCommunityChannelHandler(newChannel.Id, channelCts.Token);
                    break;
                case ChatChannel.ChatChannelType.USER:
                    UpdateAndBroadcastCount(1);
                    break;
                default:
                    UpdateAndBroadcastCount(0);
                    break;
            }
        }

        /// <summary>
        ///     Encapsulates all setup logic for handling a community channel.
        /// </summary>
        private void StartCommunityChannelHandler(ChatChannel.ChannelId channelId, CancellationToken parentCt)
        {
            communityTaskCts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
            string communityId = ChatChannel.GetCommunityIdFromChannelId(channelId);

            UniTask.RunOnThreadPool(() =>
                    CommunityCountUpdateLoopAsync(communityId, communityTaskCts.Token),
                cancellationToken: communityTaskCts.Token).Forget();
        }

        /// <summary>
        ///     The core "smart" loop that runs while the member list is visible. It periodically checks for
        ///     list changes and triggers a full refresh only when necessary.
        /// </summary>
        private async UniTaskVoid LiveMemberUpdateLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var currentChannel = currentChannelService.CurrentChannel;
                if (currentChannel == null)
                {
                    await UnifiedDelay(ct);
                    continue;
                }

                try
                {
                    switch (currentChannel.ChannelType)
                    {
                        case ChatChannel.ChatChannelType.NEARBY:
                            await UnifiedDelay(ct);
                            if (ct.IsCancellationRequested) break;

                            // For Nearby, we can get a lightweight list of IDs locally.
                            IReadOnlyCollection<string> newNearbyIds = roomHub.AllLocalRoomsRemoteParticipantIdentities();

                            // NOTE:Only trigger a full refresh if the set of members has actually changed.
                            // NOTE: we need something like this for a communities as well (to avoid situations where
                            // NOTE: one user joins and another leaves, but the count remains the same).
                            if (!lastKnownMemberIds.SetEquals(newNearbyIds))
                                await RefreshFullListAsync(ct);
                            break;

                        case ChatChannel.ChatChannelType.COMMUNITY:
                            await UnifiedDelay(ct);

                            if (ct.IsCancellationRequested) break;

                            if (lastKnownMemberCount != membersBuffer.Count)
                                await RefreshFullListAsync(ct);
                            break;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    ReportHub.LogException(ex, ReportCategory.UI);
                }
            }
        }

        /// <summary>
        ///     Fetches the full, detailed member list for the current channel, sorts it,
        ///     and broadcasts it via the OnMemberListUpdated event.
        /// </summary>
        private async UniTask RefreshFullListAsync(CancellationToken ct)
        {
            membersBuffer.Clear();
            var currentChannel = currentChannelService.CurrentChannel;
            if (currentChannel == null) return;

            try
            {
                switch (currentChannel.ChannelType)
                {
                    case ChatChannel.ChatChannelType.NEARBY:
                        FetchNearbyMembers(ct);
                        break;
                    case ChatChannel.ChatChannelType.COMMUNITY:
                        await FetchCommunityMembersAsync(currentChannel.Id, ct);
                        break;
                }

                if (ct.IsCancellationRequested) return;

                lastKnownMemberCount = membersBuffer.Count;
                lastKnownMemberIds.Clear();
                foreach (var member in membersBuffer)
                    lastKnownMemberIds.Add(member.Id);

                membersBuffer.Sort((a, b) =>
                    string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                OnMemberListUpdated?.Invoke(membersBuffer);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private void TearDownCurrentChannelListeners()
        {
            communityTaskCts?.Cancel();
            communityTaskCts?.Dispose();
            communityTaskCts = null;

            channelCts?.Cancel();
            channelCts?.Dispose();
            channelCts = null;

            // Ensure we unsubscribe from events to prevent leaks
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant -= OnParticipantUpdated;
        }


        private void SetupNearbyListeners()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant += OnParticipantUpdated;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant += OnParticipantUpdated;
            RecalculateNearbyCount();
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            RecalculateNearbyCount();
        }

        private void RecalculateNearbyCount()
        {
            int currentCount = roomHub.ParticipantsCount();
            UpdateAndBroadcastCount(currentCount);
        }

        private async UniTaskVoid CommunityCountUpdateLoopAsync(string communityId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await communitiesDataProvider
                        .GetOnlineMemberCountAsync(communityId, ct)
                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                    if (ct.IsCancellationRequested || !result.Success)
                    {
                        await UnifiedDelay(ct);
                        continue;
                    }

                    int memberCount = result.Value > 0 ? result.Value - 1 : 0;
                    if (memberCount != lastKnownMemberCount)
                    {
                        lastKnownMemberCount = memberCount;

                        OnMemberCountUpdated?.Invoke(lastKnownMemberCount);
                    }

                    await UnifiedDelay(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
                    await UnifiedDelay(ct);
                }
            }
        }

        private static UniTask UnifiedDelay(CancellationToken ct) =>
            UniTask.Delay(UNIFIED_POLL_INTERVAL_MS, DelayType.UnscaledDeltaTime, cancellationToken: ct);

        private void UpdateAndBroadcastCount(int newCount)
        {
            if (newCount == lastKnownMemberCount) return;
            lastKnownMemberCount = newCount;

            MultithreadingUtility.InvokeOnMainThread(() => OnMemberCountUpdated?.Invoke(lastKnownMemberCount));
        }

        private void FetchNearbyMembers(CancellationToken ct)
        {
            var profiles = new List<Profile>();
            GetProfilesFromParticipants(profiles);

            foreach (var profile in profiles)
            {
                if (ct.IsCancellationRequested) return;
                membersBuffer.Add(CreateMemberDataFromProfile(profile));
            }
        }

        private async UniTask FetchCommunityMembersAsync(ChatChannel.ChannelId channelId, CancellationToken ct)
        {
            string communityId = ChatChannel.GetCommunityIdFromChannelId(channelId);
            Result<GetCommunityMembersResponse> result = await communitiesDataProvider.GetOnlineCommunityMembersAsync(communityId, ct)
                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested || !result.Success) return;

            string? localPlayerAddress = web3IdentityCache.Identity?.Address;

            foreach (var memberData in result.Value.data.results)
            {
                if (ct.IsCancellationRequested) return;
                if (memberData.memberAddress == localPlayerAddress) continue;

                membersBuffer.Add(new ChatMemberListView.MemberData
                {
                    Id = memberData.memberAddress, Name = memberData.name, FaceSnapshotUrl = memberData.profilePictureUrl, ConnectionStatus = ChatMemberConnectionStatus.Online,
                    WalletId = $"#{memberData.memberAddress[^4..]}", ProfileColor = ProfileNameColorHelper.GetNameColor(memberData.name), HasClaimedName = false
                });
            }
        }

        private void GetProfilesFromParticipants(List<Profile> outProfiles)
        {
            outProfiles.Clear();
            foreach (string? identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
            {
                if (profileCache.TryGet(identity, out var profile))
                    outProfiles.Add(profile);
            }
        }

        private ChatMemberListView.MemberData CreateMemberDataFromProfile(Profile profile) =>
            new()
            {
                Id = profile.UserId, Name = profile.ValidatedName, FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl, ConnectionStatus = ChatMemberConnectionStatus.Online,
                WalletId = profile.WalletId, ProfileColor = profile.UserNameColor, HasClaimedName = profile.HasClaimedName
            };

        public void Dispose() => Stop();
    }
}
