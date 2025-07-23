
using System;

namespace DCL.Communities
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

            public void DecreaseMembersCount()
            {
                if (membersCount > 0)
                    membersCount--;
            }

            public void IncreaseMembersCount()
            {
                membersCount++;
            }
        }

        public CommunityData data;
    }
}


