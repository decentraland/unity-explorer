using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetUserInviteRequestResponse
    {
        public GetUserInviteRequestData data;
    }

    [Serializable]
    public class GetUserInviteRequestData
    {
        [Serializable]
        public class UserInviteRequestData
        {
            public string id;
            public string communityId;
            public string thumbnailUrl;
            public string memberAddress;
            public InviteRequestAction type;
            public string status;
            public string name;
            public string description;
            public string ownerAddress;
            public CommunityMemberRole role;
            public CommunityPrivacy privacy;
            public bool active;
            public int membersCount;
            public GetUserCommunitiesData.FriendInCommunity[] friends;
            public string ownerName;
        }

        public UserInviteRequestData[] results;
        public int total;
    }
}
