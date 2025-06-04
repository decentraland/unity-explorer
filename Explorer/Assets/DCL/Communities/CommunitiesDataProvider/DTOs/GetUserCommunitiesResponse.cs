
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetUserCommunitiesResponse
    {
        public GetUserCommunitiesData data;
    }

    [Serializable]
    public class GetUserCommunitiesData
    {
        [Serializable]
        public class CommunityData
        {
            public string id;
            public string[] thumbnails;
            public string name;
            public string description;
            public string ownerAddress;
            public int membersCount;
            public bool isLive;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public FriendInCommunity[] friends;
        }

        [Serializable]
        public struct FriendInCommunity
        {
            public string address;
            public string name;
            public string profilePictureUrl;
        }

        public CommunityData[] results;
        public int total;
    }


}


