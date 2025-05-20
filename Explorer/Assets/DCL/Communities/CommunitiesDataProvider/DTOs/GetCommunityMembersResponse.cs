
namespace DCL.Communities
{
    public class GetCommunityMembersResponse
    {
        public class MemberData
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


