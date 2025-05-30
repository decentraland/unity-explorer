
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityResponse
    {
        [Serializable]
        public struct CommunityData
        {
            public string id;
            public string[] thumbnails;
            public string name;
            public string description;
            public string ownerId;
            public int memberCount;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public string[] places;
        }

        public CommunityData[] community;
    }
}


