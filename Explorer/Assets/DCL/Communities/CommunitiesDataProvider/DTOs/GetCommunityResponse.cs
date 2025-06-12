
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
            public CommunityThumbnails? thumbnails;
            public string name;
            public string description;
            public string ownerAddress;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public string[] places;
            public int membersCount;
        }

        public CommunityData data;
    }
}


