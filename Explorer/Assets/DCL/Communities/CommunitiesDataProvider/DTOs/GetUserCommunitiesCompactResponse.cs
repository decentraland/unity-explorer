
namespace DCL.Communities
{
    public class GetUserCommunitiesCompactResponse
    {
        public class CommunityData
        {
            public string id;
            public string smallThumbnail;
            public string name;
            public string ownerId;
            public CommunityMemberRole role;
        }

        public CommunityData[] communities;
    }
}


