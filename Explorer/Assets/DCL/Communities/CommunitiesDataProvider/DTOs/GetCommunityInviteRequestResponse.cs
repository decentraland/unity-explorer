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
            public string communityId;
            public string memberAddress;
            public InviteRequestAction type;
            public string status;
            public string profilePictureUrl;
            public bool hasClaimedName;
            public string name;
            public FriendshipStatus friendshipStatus;
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
