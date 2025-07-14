
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
            public CommunityThumbnails? thumbnails;
            public string name;
            public string description;
            public string ownerAddress;
            public string ownerName;
            public int membersCount;
            public bool isHostingLiveEvent;
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public FriendInCommunity[] friends;

            public void SetAsJoined(bool isJoined)
            {
                // Change the role
                role = isJoined ? CommunityMemberRole.member : CommunityMemberRole.none;

                // Update the member's amount
                if (isJoined)
                    membersCount++;
                else
                    membersCount--;
            }
        }

        [Serializable]
        public struct FriendInCommunity
        {
            public string address;
            public string name;
            public string profilePictureUrl;
            public bool isVerified;
        }

        public CommunityData[] results;
        public int total;
    }
}


