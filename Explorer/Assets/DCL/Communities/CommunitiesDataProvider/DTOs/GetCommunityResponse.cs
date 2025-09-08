using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityResponse
    {
        [Serializable]
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
            public CommunityThumbnails? thumbnails;
            public string name;
            public string description;
            public string ownerAddress;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public int membersCount;
            public VoiceChatStatus voiceChatStatus;

            public string pendingInviteOrRequestId;
            public InviteRequestAction pendingActionType;

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


