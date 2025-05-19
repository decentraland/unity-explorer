
namespace DCL.Communities
{
    public class CreateOrUpdateCommunityResponse
    {
        public class CommunityData
        {
            public string[] thumbnails;
            public string name;
            public string description;
            public string ownerId;
            public int memberCount;
            public CommunityPrivacy privacy;
        }

        public bool ok;
        public CommunityData communityData;
    }
}


