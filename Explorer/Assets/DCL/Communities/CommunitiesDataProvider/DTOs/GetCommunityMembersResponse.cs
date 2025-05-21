
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityMembersResponse
    {
        [Serializable]
        public struct MemberData
        {
            public string id;
            public string profilePicture;
            public string name;
            public bool hasClaimedName;
            public CommunityMemberRole role;
            public int mutualFriends;
            public FriendshipStatus friendshipStatus;
        }

        public MemberData[] members;
        public int totalPages;
    }
}


