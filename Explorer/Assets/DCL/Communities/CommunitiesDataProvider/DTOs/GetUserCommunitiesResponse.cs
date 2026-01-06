using DCL.Profiles;
using System;
using Newtonsoft.Json;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetUserCommunitiesResponse
    {
        public GetUserCommunitiesData data;
    }

    [Serializable]
    public class GetUserCommunitiesData
    {
        [Serializable]
        public class CommunityData
        {
            public string id;
            public string thumbnailUrl;
            public string name;
            public string description;
            public string ownerAddress;
            public string ownerName;
            public int membersCount;
            public CommunityPrivacy privacy;
            public CommunityVisibility visibility;
            public CommunityMemberRole role;
            [JsonConverter(typeof(CommunitiesDTOConverters.FriendsInCommunityConverter))]
            public Profile.CompactInfo[] friends;
            [JsonConverter(typeof(VoiceChatStatusJsonConverter))]
            public GetCommunityResponse.VoiceChatStatus voiceChatStatus;

            public string inviteOrRequestId;
            public InviteRequestAction pendingActionType;
            public int requestsReceived;

            public CommunityData()
            {
            }
            public CommunityData(string id,
                string thumbnailUrl,
                string name,
                string description,
                CommunityPrivacy privacy,
                CommunityVisibility visibility,
                CommunityMemberRole role,
                string ownerAddress,
                int membersCount,
                GetCommunityResponse.VoiceChatStatus voiceChatStatus)
            {
                this.id = id;
                this.thumbnailUrl = thumbnailUrl;
                this.name = name;
                this.description = description;
                this.privacy = privacy;
                this.visibility = visibility;
                this.role = role;
                this.ownerAddress = ownerAddress;
                this.membersCount = membersCount;
                this.voiceChatStatus = voiceChatStatus;
            }

            public void SetAsJoined(bool isJoined)
            {
                // Change the role
                role = isJoined ? CommunityMemberRole.member : CommunityMemberRole.none;

                // Update the member's amount
                if (isJoined)
                    membersCount++;
                else
                    membersCount--;
            }

            public void DecreaseMembersCount()
            {
                if (membersCount > 0)
                    membersCount--;
            }
        }

        public CommunityData[] results;
        public int total;
    }
}
