using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Utilities;
using System.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Profiles.Helpers;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using Utility;
using Utility.Types;

namespace DCL.Chat.Services
{
    /// <summary>
    /// Monitors Continuously: It starts a background task (UpdateLoopAsync)
    ///     to monitor the underlying data source
    ///
    /// Efficient Polling: It checks for changes in participant count and the current island SID every 500ms.
    ///     It only rebuilds the full, detailed member list when the island changes
    ///     which is much more efficient than rebuilding it constantly.
    ///
    /// Event-Driven: It emits events (OnMemberCountUpdated, OnMemberListUpdated)
    ///     only when a change is detected. This prevents the rest of the application
    ///     from having to do any work when nothing has changed.
    ///
    /// Provides a Snapshot:
    ///     It makes the last known list of members instantly available via
    ///     the LastKnownMemberList property. This is crucial for responsiveness.
    /// </summary>

    public class ChatMemberListService : IDisposable
    {
        private readonly ICurrentChannelService currentChannelService;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly List<ChatMemberListView.MemberData> membersBuffer = new();
        public IReadOnlyList<ChatMemberListView.MemberData> LastKnownMemberList => membersBuffer;

        public event Action<IReadOnlyList<ChatMemberListView.MemberData>>? OnMemberListUpdated;
        public event Action<int> OnMemberCountUpdated;

        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly List<Profile> profilesBuffer = new();
        private CancellationTokenSource cts = new();

        // NOTE: We'll track the last known values
        // NOTE: to avoid firing events unnecessarily.
        private int lastKnownParticipantCount = -1;
        private string lastKnownIslandSid = string.Empty;
        private int lastKnownMemberCount = -1;

        public ChatMemberListService(IRoomHub roomHub,
            IProfileCache profileCache,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            ICurrentChannelService currentChannelService,
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

        public void Start()
        {
            if (!friendsServiceProxy.Configured)
            {
                ReportHub.LogError(ReportData.UNSPECIFIED, "[ChatMemberListService] FriendsService is not configured. Cannot start member list service.");
                return;
            }

            cts = cts.SafeRestart();
            Task.Run(UpdateLoopAsync);
        }

        public void Stop()
        {
            cts.SafeCancelAndDispose();
        }

        private async Task UpdateLoopAsync()
        {
            const int WAIT_TIME_MS = 1000;

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var currentChannel = currentChannelService.CurrentChannel;
                    if (currentChannel == null)
                    {
                        await Task.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
                        continue;
                    }

                    switch (currentChannel.ChannelType)
                    {
                        case ChatChannel.ChatChannelType.NEARBY:
                            await UpdateNearbyDataAsync(cts.Token);
                            break;
                        case ChatChannel.ChatChannelType.COMMUNITY:
                            await UpdateCommunityDataAsync(currentChannel.Id, cts.Token);
                            break;
                    }
                    
                    await Task.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReportHub.LogError(ReportCategory.UI, $"[{nameof(ChatMemberListService)}] UpdateLoop error: {ex}");
                }
            }
        }

        public void RequestRefresh()
        {
            GenerateAndBroadcastFullListAsync(cts.Token);
        }

        private async UniTask GenerateAndBroadcastFullListAsync(CancellationToken ct)
        {
            membersBuffer.Clear();
            profilesBuffer.Clear();

            var currentChannel = currentChannelService.CurrentChannel;
            if (currentChannel == null) return;

            switch (currentChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.NEARBY:
                    await FetchNearbyMembersAsync(ct);
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    await FetchCommunityMembersAsync(currentChannel.Id, ct);
                    break;
            }

            if (ct.IsCancellationRequested) return;

            membersBuffer.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            
            if (ct.IsCancellationRequested) return;
            
            PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, () =>
            {
                if (ct.IsCancellationRequested)
                    return;

                OnMemberListUpdated?.Invoke(membersBuffer);
            });
        }

        private async Task UpdateNearbyDataAsync(CancellationToken ct)
        {
            if (!roomHub.HasAnyRoomConnected()) return;

            int currentParticipantCount = roomHub.ParticipantsCount();
            string currentIslandSid = roomHub.IslandRoom().Info.Sid;

            if (currentParticipantCount != lastKnownMemberCount)
            {
                lastKnownMemberCount = currentParticipantCount;
                await UniTask.SwitchToMainThread(ct);
                OnMemberCountUpdated?.Invoke(lastKnownMemberCount);
            }

            if (currentIslandSid != lastKnownIslandSid)
            {
                lastKnownIslandSid = currentIslandSid;
                await GenerateAndBroadcastFullListAsync(ct);
            }
        }

        private async Task UpdateCommunityDataAsync(ChatChannel.ChannelId channelId, CancellationToken ct)
        {
            string communityId = ChatChannel.GetCommunityIdFromChannelId(channelId);
            var result = await communitiesDataProvider.GetOnlineMemberCountAsync(communityId, ct)
                .SuppressToResultAsync(ReportCategory.COMMUNITIES);
            if (ct.IsCancellationRequested || !result.Success) return;

            int memberCount = result.Value > 0 ? result.Value - 1 : 0;
            if (memberCount != lastKnownMemberCount)
            {
                lastKnownMemberCount = memberCount;
                await UniTask.SwitchToMainThread(ct);
                OnMemberCountUpdated?.Invoke(lastKnownMemberCount);
            }
        }

        private async UniTask FetchNearbyMembersAsync(CancellationToken ct)
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

        private ChatMemberListView.MemberData CreateMemberDataFromProfile(Profile profile)
        {
            return new ChatMemberListView.MemberData
            {
                Id = profile.UserId, Name = profile.ValidatedName, FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl, ConnectionStatus = ChatMemberConnectionStatus.Online,
                WalletId = profile.WalletId, ProfileColor = profile.UserNameColor, HasClaimedName = profile.HasClaimedName
            };
        }

        public void Dispose() => Stop();
    }
}
