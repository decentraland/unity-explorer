
using System;

namespace DCL.Communities
{
    [Serializable]
    public class CreateOrUpdateCommunityResponse
    {
        [Serializable]
        public struct CommunityData
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


