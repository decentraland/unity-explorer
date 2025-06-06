
using DCL.Profiles;
using DCL.Profiles.Helpers;
using System.Text.RegularExpressions;
using UnityEngine;
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityMembersResponse
    {
        [Serializable]
        public class MemberData
        {
            public string communityId;
            public string memberAddress;
            public CommunityMemberRole role;
            public string joinedAt;
            public string profilePictureUrl;
            public bool hasClaimedName;
            public string name;
            public int mutualFriends;
            public FriendshipStatus friendshipStatus;

            private Color userNameColor;

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
    }
}


