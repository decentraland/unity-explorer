using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetOnlineCommunityMembersResponse
    {
        [Serializable]
        public struct MemberData
        {
            public string id;
            public string name;
            public string profilePicture;
            public FriendshipStatus friendshipStatus;
            public CommunityMemberRole role;
        }

        public MemberData[] members;
    }
}


