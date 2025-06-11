using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatControllerMemberListHelper
    {
        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;
        private readonly List<ChatMemberListView.MemberData> membersBuffer;
        private readonly List<Profile> participantProfileBuffer;
        private readonly ChatController controller;
        private CancellationTokenSource memberListCts = new();
        private ChatView viewInstance;

        public ChatControllerMemberListHelper(
            IRoomHub roomHub,
            IProfileCache profileCache,
            List<ChatMemberListView.MemberData> membersBuffer,
            List<Profile> participantProfileBuffer,
            ChatController controller)
        {
            this.roomHub = roomHub;
            this.profileCache = profileCache;
            this.membersBuffer = membersBuffer;
            this.participantProfileBuffer = participantProfileBuffer;
            this.controller = controller;
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

        private List<ChatMemberListView.MemberData> GenerateMemberList()
        {
            membersBuffer.Clear();
            GetProfilesFromParticipants(participantProfileBuffer);

            for (int i = 0; i < participantProfileBuffer.Count; ++i)
            {
                ChatMemberListView.MemberData newMember = GetMemberDataFromParticipantIdentity(participantProfileBuffer[i]);
                if (!string.IsNullOrEmpty(newMember.Name))
                    membersBuffer.Add(newMember);
            }

            return membersBuffer;
        }

        private ChatMemberListView.MemberData GetMemberDataFromParticipantIdentity(Profile profile)
        {
            ChatMemberListView.MemberData newMemberData = new ChatMemberListView.MemberData
            {
                Id = profile.UserId,
            };

            if (profile != null)
            {
                newMemberData.Name = profile.ValidatedName;
                newMemberData.FaceSnapshotUrl = profile.Avatar.FaceSnapshotUrl;
                newMemberData.ConnectionStatus = ChatMemberConnectionStatus.Online; // TODO: Get this info from somewhere, when the other shapes are developed
                newMemberData.WalletId = profile.WalletId;
                newMemberData.ProfileColor = profile.UserNameColor;
            }

            return newMemberData;
        }

        public void RefreshMemberList()
        {
            List<ChatMemberListView.MemberData> members = GenerateMemberList();
            viewInstance.SetMemberData(members);
        }

        private void GetProfilesFromParticipants(List<Profile> outProfiles)
        {
            outProfiles.Clear();
            foreach (string? identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
            {
                // TODO: Use new endpoint to get a bunch of profile info
                if (profileCache.TryGet(identity, out var profile))
                    outProfiles.Add(profile);
            }
        }

        private async UniTask UpdateMembersDataAsync()
        {
            const int WAIT_TIME_IN_BETWEEN_UPDATES = 500;

            while (!memberListCts.IsCancellationRequested)
            {
                // If the player jumps to another island room (like a world) while the member list is visible, it must refresh
                if (controller.PreviousRoomSid != controller.IslandRoomSid && viewInstance?.IsMemberListVisible == true)
                {
                    controller.PreviousRoomSid = controller.IslandRoomSid;
                    RefreshMemberList();
                }

                // Updates the amount of members
                int participantsCount = roomHub.ParticipantsCount();

                if (roomHub.HasAnyRoomConnected() && viewInstance != null && participantsCount != viewInstance.MemberCount)
                    viewInstance.MemberCount = participantsCount;

                await UniTask.Delay(WAIT_TIME_IN_BETWEEN_UPDATES, cancellationToken: memberListCts.Token);
            }
        }

        public void Dispose()
        {
            StopUpdating();
        }
    }
}
