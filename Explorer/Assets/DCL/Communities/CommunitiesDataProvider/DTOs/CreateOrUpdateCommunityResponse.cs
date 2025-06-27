
using System;

namespace DCL.Communities
{
    [Serializable]
    public class CreateOrUpdateCommunityResponse
    {
        [Serializable]
        public struct CommunityData
        {
            public string id;
            public string name;
            public string description;
            public string ownerAddress;
            public CommunityPrivacy privacy;
            public bool active;
            public CommunityThumbnails? thumbnails;
        }

        public string message;
        public CommunityData data;
    }
}


