using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Utilities;
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
        public event Action<int>? OnMemberCountUpdated;

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
                return;

            cts = cts.SafeRestart();
            UniTask.RunOnThreadPool(UpdateLoopAsync).Forget();
        }

        public void Stop()
        {
            cts.SafeCancelAndDispose();
        }

        private async UniTask UpdateLoopAsync()
        {
            const int WAIT_TIME_MS = 500;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (!roomHub.HasAnyRoomConnected())
                    {
                        await UniTask.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
                        continue;
                    }
                    
                    int currentParticipantCount = roomHub.ParticipantsCount();
                    string currentIslandSid = roomHub.IslandRoom().Info.Sid;

                    if (currentIslandSid != lastKnownIslandSid ||
                        currentParticipantCount != lastKnownParticipantCount)
                    {
                        ReportHub.Log(ReportData.UNSPECIFIED, $"Member list change detected. Island: '{lastKnownIslandSid}' -> '{currentIslandSid}'. Count: {lastKnownParticipantCount} -> {currentParticipantCount}. Refreshing.");
                
                        lastKnownIslandSid = currentIslandSid;
                        lastKnownParticipantCount = currentParticipantCount;

                        await GenerateAndBroadcastFullListAsync(cts.Token);
                    }

                    await UniTask.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReportHub.LogError(ReportData.UNSPECIFIED, $"[ChatMemberListService] UpdateLoop error: {ex}");
                }
            }
        }


        public async UniTask RequestRefreshAsync()
        {
            await GenerateAndBroadcastFullListAsync(cts.Token);
        }

        private async UniTask GenerateAndBroadcastFullListAsync(CancellationToken ct)
        {
            List<ChatMemberListView.MemberData> newMembers = new();
            profilesBuffer.Clear();

            GetProfilesFromParticipants(profilesBuffer);

            foreach (var profile in profilesBuffer)
            {
                if (ct.IsCancellationRequested) return;
                newMembers.Add(CreateMemberDataFromProfile(profile));
            }

            newMembers.Sort((a, b) =>
                string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            await UniTask.SwitchToMainThread(ct);
            if (ct.IsCancellationRequested) return;

            membersBuffer.Clear();
            membersBuffer.AddRange(newMembers);

            ReportHub.Log(ReportCategory.UNSPECIFIED, $"Broadcasting updated member list for World '{lastKnownIslandSid}'. Count: {membersBuffer.Count}\n");
            foreach (var member in membersBuffer)
                ReportHub.Log(ReportCategory.UNSPECIFIED,$"  - {member.Name} ({member.Id})");
            
            OnMemberListUpdated?.Invoke(membersBuffer);
            OnMemberCountUpdated?.Invoke(membersBuffer.Count);
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