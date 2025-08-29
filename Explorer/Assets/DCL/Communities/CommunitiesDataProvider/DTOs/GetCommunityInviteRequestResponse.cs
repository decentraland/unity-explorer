using DCL.Profiles.Helpers;
using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityInviteRequestResponse : ICommunityMemberPagedResponse
    {
        [Serializable]
        public class CommunityInviteRequestData : ICommunityMemberData
        {
            public string id;
            public string communityId;
            public string memberAddress;
            public InviteRequestAction type;
            public string status;
            public string name;
            public bool hasClaimedName;
            public string profilePictureUrl;
            public FriendshipStatus friendshipStatus;

            public string Id => id;
            public string Address => memberAddress;
            public string ProfilePictureUrl => profilePictureUrl;
            public bool HasClaimedName => hasClaimedName;
            public string Name => name;
            public int MutualFriends => 0;

            public CommunityMemberRole Role
            {
                get => role;
                set => role = value;
            }

            public FriendshipStatus FriendshipStatus
            {
                get => friendshipStatus;
                set => friendshipStatus = value;
            }

            private CommunityMemberRole role = CommunityMemberRole.none;

            public Color GetUserNameColor() =>
                ProfileNameColorHelper.GetNameColor(name);
        }

        [Serializable]
        public class GetCommunityInviteRequestResponseData
        {
            public CommunityInviteRequestData[] results;
            public int total;
            public int limit;
            public int offset;
        }

        public GetCommunityInviteRequestResponseData data;
        public ICommunityMemberData[] members => data.results;
        public int total => data.total;
    }
}
