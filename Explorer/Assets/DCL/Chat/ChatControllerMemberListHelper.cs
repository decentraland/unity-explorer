using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.UI;

namespace DCL.Chat
{
    public class ChatControllerMemberListHelper
    {
        private readonly IRoomHub roomHub;
        private readonly IProfileCache profileCache;
        private readonly List<ChatMemberListView.MemberData> membersBuffer;
        private readonly List<Profile> participantProfileBuffer;
        private readonly Func<string> getIslandRoomSid;
        private readonly Func<ChatView?> getViewInstance;
        private readonly Func<string> getPreviousRoomSid;
        private readonly Action<string> setPreviousRoomSid;
        private readonly Func<CancellationTokenSource> getMemberListCts;

        public ChatControllerMemberListHelper(
            IRoomHub roomHub,
            IProfileCache profileCache,
            List<ChatMemberListView.MemberData> membersBuffer,
            List<Profile> participantProfileBuffer,
            Func<string> getIslandRoomSid,
            Func<ChatView?> getViewInstance,
            Func<string> getPreviousRoomSid,
            Action<string> setPreviousRoomSid,
            Func<CancellationTokenSource> getMemberListCts)
        {
            this.roomHub = roomHub;
            this.profileCache = profileCache;
            this.membersBuffer = membersBuffer;
            this.participantProfileBuffer = participantProfileBuffer;
            this.getIslandRoomSid = getIslandRoomSid;
            this.getViewInstance = getViewInstance;
            this.getPreviousRoomSid = getPreviousRoomSid;
            this.setPreviousRoomSid = setPreviousRoomSid;
            this.getMemberListCts = getMemberListCts;
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
            getViewInstance()?.SetMemberData(members);
        }

        private void GetProfilesFromParticipants(List<Profile> outProfiles)
        {
            outProfiles.Clear();
            foreach (string? identity in roomHub.AllRoomsRemoteParticipantIdentities())
            {
                // TODO: Use new endpoint to get a bunch of profile info
                if (profileCache.TryGet(identity, out var profile))
                    outProfiles.Add(profile);
            }
        }

        public async UniTask UpdateMembersDataAsync()
        {
            const int WAIT_TIME_IN_BETWEEN_UPDATES = 500;
            var memberListCts = getMemberListCts();

            while (!memberListCts.IsCancellationRequested)
            {
                var viewInstance = getViewInstance();
                // If the player jumps to another island room (like a world) while the member list is visible, it must refresh
                if (getPreviousRoomSid() != getIslandRoomSid() && viewInstance?.IsMemberListVisible == true)
                {
                    setPreviousRoomSid(getIslandRoomSid());
                    RefreshMemberList();
                }

                // Updates the amount of members
                int participantsCount = roomHub.ParticipantsCount();
                if (roomHub.HasAnyRoomConnected() && viewInstance != null && participantsCount != viewInstance.MemberCount)
                    viewInstance.MemberCount = participantsCount;

                await UniTask.Delay(WAIT_TIME_IN_BETWEEN_UPDATES, cancellationToken: memberListCts.Token);
            }
        }
    }
} 