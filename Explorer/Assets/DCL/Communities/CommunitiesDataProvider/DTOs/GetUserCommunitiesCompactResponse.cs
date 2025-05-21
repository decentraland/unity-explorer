
using System;

namespace DCL.Communities
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
            public CommunityMemberRole role;
        }

        public CommunityData[] communities;
    }
}


