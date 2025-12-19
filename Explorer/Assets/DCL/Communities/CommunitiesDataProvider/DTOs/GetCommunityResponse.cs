using System;
using Newtonsoft.Json;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityResponse
    {
        [Serializable]
        [JsonConverter(typeof(VoiceChatStatusJsonConverter))]
        public struct VoiceChatStatus
        {
            public bool isActive;
            public int participantCount;
            public int moderatorCount;
        }

        [Serializable]
        public struct CommunityData
        {
            public string id;
            public string thumbnailUrl;
            public string name;
            public string description;
            public string ownerAddress;
            public CommunityPrivacy privacy;
            public CommunityVisibility visibility;
            public CommunityMemberRole role;
            public int membersCount;
            public VoiceChatStatus voiceChatStatus;

            public string pendingInviteOrRequestId;
            public InviteRequestAction pendingActionType;

            public bool isSubscribedToNotifications;

            public void DecreaseMembersCount()
            {
                if (membersCount > 0)
                    membersCount--;
            }

            public void IncreaseMembersCount() =>
                membersCount++;

            public void SetRole(CommunityMemberRole newRole) =>
                role = newRole;

            public void SetPendingInviteOrRequestId(string inviteOrRequestId) =>
                pendingInviteOrRequestId = inviteOrRequestId;

            public void SetPendingAction(InviteRequestAction action) =>
                pendingActionType = action;

            public bool IsAccessAllowed() =>
                privacy == CommunityPrivacy.@public || (privacy == CommunityPrivacy.@private && role != CommunityMemberRole.none);
        }

        public CommunityData data;
    }
}


