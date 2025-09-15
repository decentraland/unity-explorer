using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetUserCommunitiesCompactResponse
    {
        [Serializable]
        public struct CommunityData
        {
            public string id;
            public string smallThumbnail;
            public string name;
            public string ownerId;
            public int memberCount;
            public CommunityMemberRole role;
        }

        public CommunityData[] communities;
    }
}


