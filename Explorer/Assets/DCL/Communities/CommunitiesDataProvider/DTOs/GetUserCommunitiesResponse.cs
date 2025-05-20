
namespace DCL.Communities
{
    public class GetUserCommunitiesResponse
    {
        public class FriendInCommunity
        {
            public string id;
            public string name;
            public string profilePictureUrl;
        }

        public class CommunityData
        {
            public string id;
            public string[] thumbnails;
            public string name;
            public string description;
            public string ownerId;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public FriendInCommunity[] friends;
        }

        public CommunityData[] communities;
        public int totalPages;
    }
}


