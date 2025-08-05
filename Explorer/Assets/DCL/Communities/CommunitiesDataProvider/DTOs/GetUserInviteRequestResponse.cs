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
            public CommunityThumbnails? thumbnails;
            public string name;
            public string description;
            public string ownerAddress;
            public string ownerName;
            public int membersCount;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public GetUserCommunitiesData.FriendInCommunity[] friends;
            public InviteRequestAction action;
        }

        public UserInviteRequestData[] results;
        public int total;
    }
}
