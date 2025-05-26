
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetUserCommunitiesResponse
    {
        [Serializable]
        public struct FriendInCommunity
        {
            public string id;
            public string name;
            public string profilePictureUrl;
        }

        [Serializable]
        public class CommunityData
        {
            public string id;
            public string[] thumbnails;
            public string name;
            public string description;
            public string ownerId;
            public int memberCount;
            public bool isLive;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public FriendInCommunity[] friends;
        }

        public CommunityData[] communities;
        public int totalAmount;
    }
}


