using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityInviteRequestResponse
    {
        [Serializable]
        public class CommunityInviteRequestData
        {
            public string id;
            public string address;
            public string profilePictureUrl;
            public bool hasClaimedName;
            public string name;
            public string requestedAt;
        }

        [Serializable]
        public class GetCommunityInviteRequestResponseData
        {
            public CommunityInviteRequestData[] results;
            public int total;
        }

        public GetCommunityInviteRequestResponseData data;
    }
}
