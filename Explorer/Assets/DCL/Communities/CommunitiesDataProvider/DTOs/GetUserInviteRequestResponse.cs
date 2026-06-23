using DCL.Profiles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetUserInviteRequestResponse
    {
        public GetUserInviteRequestData data;
    }

    [Serializable]
    public class GetUserInviteRequestData
    {
        [Serializable]
        public class UserInviteRequestData
        {
            public string id;
            public string communityId;
            public string thumbnailUrl;
            public string memberAddress;
            public InviteRequestAction type;
            public string status;
            public string name;
            public string description;
            public string ownerAddress;
            public CommunityMemberRole role;
            public CommunityPrivacy privacy;
            public bool active;
            public int membersCount;
            [JsonProperty("friends")] internal string[] friendAddresses;
            [JsonIgnore] public IReadOnlyList<Profile.CompactInfo> Friends { get; internal set; } = Array.Empty<Profile.CompactInfo>();
            public string ownerName;
        }

        public UserInviteRequestData[] results = Array.Empty<UserInviteRequestData>();
        public int total;
    }
}
