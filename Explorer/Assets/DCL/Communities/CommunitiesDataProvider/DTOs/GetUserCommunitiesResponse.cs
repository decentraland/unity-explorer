using DCL.Profiles;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    // Server schema: social-service-ea docs/schemas.yaml#/components/schemas/GetCommunitiesV2200OkResponse
    [Serializable]
    public class GetUserCommunitiesResponse
    {
        public GetUserCommunitiesData data = null!;
    }

    [Serializable]
    public class GetUserCommunitiesData
    {
        [Serializable]
        public class CommunityData
        {
            public string id = null!;
            public string thumbnailUrl = null!;
            public string name = null!;
            public string description = null!;
            public string ownerAddress = null!;
            public int membersCount;
            public CommunityPrivacy privacy;
            public CommunityVisibility visibility;
            public CommunityMemberRole role = CommunityMemberRole.none;

            [JsonIgnore] public string OwnerName { get; internal set; } = string.Empty;

            // Optional per schema (present only when signed in) — left nullable, no non-null initialization; hydrated into Friends client-side.
            [JsonProperty("friends")] public string[]? friendAddresses;
            [JsonIgnore] public IReadOnlyList<Profile.CompactInfo> Friends { get; internal set; } = Array.Empty<Profile.CompactInfo>();
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

        public CommunityData[] results = Array.Empty<CommunityData>();
        public int total;
    }
}
