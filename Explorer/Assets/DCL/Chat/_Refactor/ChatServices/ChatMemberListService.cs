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
using Utility;

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
        private readonly List<ChatMemberListView.MemberData> membersBuffer = new();
        public IReadOnlyList<ChatMemberListView.MemberData> LastKnownMemberList => membersBuffer;

        public event Action<IReadOnlyList<ChatMemberListView.MemberData>>? OnMemberListUpdated;

        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly List<Profile> profilesBuffer = new();
        private CancellationTokenSource cts = new();

        // NOTE: We'll track the last known values
        // NOTE: to avoid firing events unnecessarily.
        private int lastKnownParticipantCount = -1;
        private string lastKnownIslandSid = string.Empty;

        public ChatMemberListService(IRoomHub roomHub,
            IProfileCache profileCache,
            ObjectProxy<IFriendsService> friendsServiceProxy)
        {
            this.roomHub = roomHub;
            this.profileCache = profileCache;
            this.friendsServiceProxy = friendsServiceProxy;
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
            const int WAIT_TIME_MS = 500;

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (!roomHub.HasAnyRoomConnected())
                    {
                        await Task.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
                        continue;
                    }

                    int currentParticipantCount = roomHub.ParticipantsCount();
                    string currentIslandSid = roomHub.IslandRoom().Info.Sid;

                    if (currentIslandSid != lastKnownIslandSid ||
                        currentParticipantCount != lastKnownParticipantCount)
                    {
                        // ReportHub.Log(ReportCategory.UI, $"Member list change detected. Island: '{lastKnownIslandSid}' -> '{currentIslandSid}'. Count: {lastKnownParticipantCount} -> {currentParticipantCount}. Refreshing.");

                        lastKnownIslandSid = currentIslandSid;
                        lastKnownParticipantCount = currentParticipantCount;

                        GenerateAndBroadcastFullListAsync(cts.Token);
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

        private void GenerateAndBroadcastFullListAsync(CancellationToken ct)
        {
            membersBuffer.Clear();
            profilesBuffer.Clear();

            GetProfilesFromParticipants(profilesBuffer);

            foreach (var profile in profilesBuffer)
            {
                if (ct.IsCancellationRequested) return;
                membersBuffer.Add(CreateMemberDataFromProfile(profile));
            }

            membersBuffer.Sort(static (a, b) =>
                string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            if (ct.IsCancellationRequested) return;

            PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, () =>
            {
                if (ct.IsCancellationRequested)
                    return;

                OnMemberListUpdated?.Invoke(membersBuffer);
            });
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
