using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities.Extensions;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Chat
{
    public class ChatControllerMemberListHelper
    {
        private readonly IRoomHub roomHub;
        private readonly List<ChatUserData> membersBuffer;
        private readonly List<ChatUserData> participantProfileBuffer;
        private readonly ChatController controller;
        private readonly IChatHistory chatHistory;
        private readonly Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communities;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private CancellationTokenSource memberListCts = new();
        private ChatView viewInstance;
        private GetChannelMembersDelegate getParticipantProfiles;
        private CancellationTokenSource memberCountCts;

        public ChatControllerMemberListHelper(
            IRoomHub roomHub,
            List<ChatUserData> membersBuffer,
            GetChannelMembersDelegate getParticipantProfiles,
            List<ChatUserData> participantProfileBuffer,
            ChatController controller,
            IChatHistory chatHistory,
            Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communities,
            ICommunitiesDataProvider communitiesDataProvider)
        {
            this.roomHub = roomHub;
            this.membersBuffer = membersBuffer;
            this.participantProfileBuffer = participantProfileBuffer;
            this.controller = controller;
            this.getParticipantProfiles = getParticipantProfiles;
            this.chatHistory = chatHistory;
            this.communities = communities;
            this.communitiesDataProvider = communitiesDataProvider;
        }

        public void SetView(ChatView view)
        {
            viewInstance = view;
        }

        public void StartUpdating()
        {
            memberListCts = memberListCts.SafeRestart();
            UniTask.RunOnThreadPool(UpdateMembersDataAsync).Forget();
        }

        public void StopUpdating()
        {
            memberListCts.SafeCancelAndDispose();
        }

        private async UniTask<List<ChatUserData>> GenerateMemberListAsync(CancellationToken ct)
        {
            membersBuffer.Clear();
            await getParticipantProfiles(participantProfileBuffer, ct);

            for (int i = 0; i < participantProfileBuffer.Count; ++i)
            {
                ChatUserData newMember = GetMemberDataFromParticipantIdentity(participantProfileBuffer[i]);
                if (!string.IsNullOrEmpty(newMember.Name))
                    membersBuffer.Add(newMember);
            }

            return membersBuffer;
        }

        private ChatUserData GetMemberDataFromParticipantIdentity(ChatUserData profile)
        {
            ChatUserData newMemberData = new ChatUserData
            {
                WalletAddress = profile.WalletAddress,
            };

            newMemberData.Name = profile.Name;
            newMemberData.FaceSnapshotUrl = profile.FaceSnapshotUrl;
            newMemberData.ConnectionStatus = profile.ConnectionStatus;
            newMemberData.WalletId = profile.WalletId;
            newMemberData.ProfileColor = profile.ProfileColor;

            return newMemberData;
        }

        public async UniTask RefreshMemberListAsync(CancellationToken ct)
        {
            List<ChatUserData> members = await GenerateMemberListAsync(ct);
            viewInstance.SetMemberData(members);
        }

        private async UniTask UpdateMembersDataAsync()
        {
            const int WAIT_TIME_IN_BETWEEN_UPDATES = 500;

            while (!memberListCts.IsCancellationRequested)
            {
                if (chatHistory.Channels[viewInstance.CurrentChannelId].ChannelType == ChatChannel.ChatChannelType.NEARBY)
                {
                    // If the player jumps to another island room (like a world) while the member list is visible, it must refresh
                    if (controller.PreviousRoomSid != controller.IslandRoomSid && viewInstance?.IsMemberListVisible == true)
                    {
                        controller.PreviousRoomSid = controller.IslandRoomSid;
                        await RefreshMemberListAsync(memberListCts.Token);
                    }

                    // Updates the amount of members
                    int participantsCount = roomHub.ParticipantsCount();

                    if (roomHub.HasAnyRoomConnected() && viewInstance != null && participantsCount != viewInstance.MemberCount)
                        viewInstance.MemberCount = participantsCount;
                }
                else if(chatHistory.Channels[viewInstance.CurrentChannelId].ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                {
                    memberCountCts = memberCountCts.SafeRestart();
                    await RefreshCommunityMemberCountAsync(communities[viewInstance.CurrentChannelId].id, communitiesDataProvider, memberCountCts.Token);
                }

                await UniTask.Delay(WAIT_TIME_IN_BETWEEN_UPDATES, cancellationToken: memberListCts.Token);
            }
        }

        public async UniTask RefreshCommunityMemberCountAsync(string communityId, ICommunitiesDataProvider dataProvider, CancellationToken ct)
        {
             Result<int> result = await dataProvider.GetOnlineMemberCountAsync(communityId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

             if (ct.IsCancellationRequested)
                 return;

             if (result.Success)
             {
                 viewInstance.MemberCount = result.Value - 1;
             }
        }

        public void Dispose()
        {
            StopUpdating();
        }
    }
}
