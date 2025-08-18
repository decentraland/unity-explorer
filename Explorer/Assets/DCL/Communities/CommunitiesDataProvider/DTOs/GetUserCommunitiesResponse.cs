using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
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
            public CommunityPrivacy privacy;
            public CommunityMemberRole role;
            public FriendInCommunity[] friends;

            public string inviteOrRequestId;
            public InviteRequestAction pendingActionType;
            public int requestsReceived;

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

            public void DecreaseMembersCount()
            {
                if (membersCount > 0)
                    membersCount--;
            }
        }

        [Serializable]
        public struct FriendInCommunity
        {
            public string address;
            public string name;
            public string profilePictureUrl;
            public bool hasClaimedName;
        }

        public CommunityData[] results;
        public int total;
    }
}


