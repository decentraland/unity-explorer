using DCL.Profiles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    // Server schema: social-service-ea docs/schemas.yaml#/components/schemas/GetMemberRequestsV2200OkResponse
    [Serializable]
    public class GetUserInviteRequestResponse
    {
        public GetUserInviteRequestData data = null!;
    }

    [Serializable]
    public class GetUserInviteRequestData
    {
        [Serializable]
        public class UserInviteRequestData
        {
            public string id = null!;
            public string communityId = null!;
            public string thumbnailUrl = null!;
            public string memberAddress = null!;
            public InviteRequestAction type;
            public string status = null!;
            public string name = null!;
            public string description = null!;
            public string ownerAddress = null!;
            public CommunityMemberRole role;
            public CommunityPrivacy privacy;
            public bool active;
            public int membersCount;
            [JsonProperty("friends")] internal string[] friendAddresses = null!;
            [JsonIgnore] public IReadOnlyList<Profile.CompactInfo> Friends { get; internal set; } = Array.Empty<Profile.CompactInfo>();
            [JsonIgnore] public string OwnerName { get; internal set; } = string.Empty;
        }

        public UserInviteRequestData[] results = Array.Empty<UserInviteRequestData>();
        public int total;
    }
}
