using DCL.Profiles;
using DCL.Profiles.Helpers;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityMembersResponse : ICommunityMemberPagedResponse
    {
        [Serializable]
        public class MemberData : ICommunityMemberData
        {
            public string communityId;
            public string memberAddress;
            public CommunityMemberRole role;
            public string joinedAt;
            public string profilePictureUrl;
            public bool hasClaimedName;
            public string name;
            public int mutualFriends;

            public FriendshipStatus friendshipStatus
            {
                get
                {
                    if (friendStatus is FriendshipStatus.deleted or FriendshipStatus.canceled or FriendshipStatus.rejected)
                        return FriendshipStatus.none;

                    return friendStatus;
                }

                set => friendStatus = value;
            }

            private FriendshipStatus friendStatus;
            private Color userNameColor;

            public string Id => communityId;
            public string Address => memberAddress;
            public string ProfilePictureUrl => profilePictureUrl;
            public bool HasClaimedName => hasClaimedName;
            public string Name => name;
            public int MutualFriends => mutualFriends;
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

            public Color GetUserNameColor()
            {
                if (userNameColor == default(Color))
                    userNameColor = CalculateUserNameColor();

                return userNameColor;
            }

            private Color CalculateUserNameColor()
            {
                string displayName = string.Empty;

                if (string.IsNullOrEmpty(name))
                    return ProfileNameColorHelper.GetNameColor(name);

                string result = string.Empty;
                MatchCollection matches = Profile.VALID_NAME_CHARACTERS.Matches(name);

                foreach (Match match in matches)
                    result += match.Value;

                displayName = result;

                if (!hasClaimedName && !string.IsNullOrEmpty(memberAddress) && memberAddress.Length > 4)
                    displayName = $"{result}{memberAddress}";

                return ProfileNameColorHelper.GetNameColor(displayName);
            }

            public override int GetHashCode() =>
                memberAddress.GetHashCode();

            private bool Equals(MemberData other) =>
                memberAddress.Equals(other.memberAddress);

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MemberData) obj);
            }
        }

        [Serializable]
        public class GetCommunityMembersResponseData
        {
            public MemberData[] results;
            public int total;
            public int page;
            public int pages;
            public int limit;
        }

        public GetCommunityMembersResponseData data;
        public ICommunityMemberData[] members => data.results;
        public int total => data.total;
    }
}


