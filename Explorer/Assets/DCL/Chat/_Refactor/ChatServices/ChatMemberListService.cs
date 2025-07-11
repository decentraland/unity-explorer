using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Utilities;
using Utility;

public class ChatMemberListService : IDisposable
{
    private readonly IRoomHub roomHub;
    private readonly IProfileCache profileCache;
    private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
    private CancellationTokenSource cts = new();
    
    public event Action<IReadOnlyList<ChatMemberListView.MemberData>>? OnMemberListUpdated;
    public event Action<int>? OnMemberCountUpdated;
    
    private readonly List<ChatMemberListView.MemberData> membersBuffer = new();
    private readonly List<Profile> profilesBuffer = new();

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
            try // CHANGED: Added exception handling to keep loop alive
            {
                if (!roomHub.HasAnyRoomConnected())
                {
                    await UniTask.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
                    continue;
                }

                // 1. Participant count change
                int currentParticipantCount = roomHub.ParticipantsCount();
                if (currentParticipantCount != lastKnownParticipantCount)
                {
                    lastKnownParticipantCount = currentParticipantCount;
                    // CHANGED: Ensure main-thread before invoking UI-related events
                    await UniTask.SwitchToMainThread(cts.Token);
                    if (cts.IsCancellationRequested) continue;
                    OnMemberCountUpdated?.Invoke(currentParticipantCount);
                }

                // 2. Island SID change -> full list refresh
                string currentIslandSid = roomHub.IslandRoom().Info.Sid;
                if (currentIslandSid != lastKnownIslandSid)
                {
                    lastKnownIslandSid = currentIslandSid;
                    await GenerateAndBroadcastFullListAsync(cts.Token);
                }

                await UniTask.Delay(WAIT_TIME_MS, cancellationToken: cts.Token);
            }
            catch (Exception ex)
            {
                // CHANGED: Log error and continue polling
                ReportHub.LogError(ReportData.UNSPECIFIED, $"[ChatMemberListService] UpdateLoop error: {ex}");
            }
        }
    }


    // This method is now explicitly for full refreshes.
    public async UniTask RefreshMemberListAsync()
    {
        await GenerateAndBroadcastFullListAsync(cts.Token);
    }
    
    private async UniTask GenerateAndBroadcastFullListAsync(CancellationToken ct)
    {
        membersBuffer.Clear();
        profilesBuffer.Clear();

        GetProfilesFromParticipants(profilesBuffer);

        foreach (var profile in profilesBuffer)
        {
            if (ct.IsCancellationRequested) return;
            membersBuffer.Add(CreateMemberDataFromProfile(profile));
        }
        
        await UniTask.SwitchToMainThread(ct);
        if (ct.IsCancellationRequested) return;

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
            Id = profile.UserId,
            Name = profile.ValidatedName,
            FaceSnapshotUrl= profile.Avatar.FaceSnapshotUrl,
            
            // TODO: wire real status when available
            ConnectionStatus= ChatMemberConnectionStatus.Online,
            
            WalletId = profile.WalletId,
            ProfileColor = profile.UserNameColor
        };
    }

    public void Dispose() => Stop();
}