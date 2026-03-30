using DCL.Profiles;
using Newtonsoft.Json;
using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityInviteRequestResponse : ICommunityMemberPagedResponse
    {
        [Serializable]
        [JsonConverter(typeof(CommunitiesDTOConverters.CommunityInviteRequestDataConverter))]
        public class CommunityInviteRequestData : ICommunityMemberData
        {
            public string id;

            public Profile.CompactInfo Profile { get; internal set; }

            public string communityId;
            public InviteRequestAction type;
            public string status;
            public FriendshipStatus friendshipStatus;

            public string Id => id;
            public int MutualFriends => 0;

            public CommunityMemberRole Role
            {
                get => role;
                set => role = value;
            }

            public FriendshipStatus FriendshipStatus
            {
                get => friendshipStatus;
                set => friendshipStatus = value;
            }

            private CommunityMemberRole role = CommunityMemberRole.none;
        }

        [Serializable]
        public class GetCommunityInviteRequestResponseData
        {
            public CommunityInviteRequestData[] results;
            public int total;
            public int limit;
            public int offset;
        }

        public GetCommunityInviteRequestResponseData data;
        public ICommunityMemberData[] members => data.results;
        public int total => data.total;
    }
}
