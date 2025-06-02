
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Utility;
using System;
using System.Linq;

namespace DCL.Communities
{
    [Serializable]
    public class GetCommunityMembersResponse
    {
        [Serializable]
        public class MemberData
        {
            public readonly string id;
            public readonly string profilePicture;
            public readonly string name;
            public readonly bool hasClaimedName;
            public CommunityMemberRole role;
            public int mutualFriends;
            public FriendshipStatus friendshipStatus;
            public Color UserNameColor;

            public MemberData(string id, string profilePicture, string name, bool hasClaimedName)
            {
                this.id = id;
                this.profilePicture = profilePicture;
                this.name = name;
                this.hasClaimedName = hasClaimedName;

                SetUserNameColor();
            }

            public MemberData(string id, string profilePicture, string name, bool hasClaimedName, CommunityMemberRole role,
                int mutualFriends, FriendshipStatus friendshipStatus)
            {
                this.id = id;
                this.profilePicture = profilePicture;
                this.name = name;
                this.hasClaimedName = hasClaimedName;
                this.role = role;
                this.mutualFriends = mutualFriends;
                this.friendshipStatus = friendshipStatus;
                this.UserNameColor = Color.white;

                SetUserNameColor();
            }

            private void SetUserNameColor()
            {
                string displayName = string.Empty;

                if (string.IsNullOrEmpty(name)) return;

                string result = string.Empty;
                MatchCollection matches = Profile.VALID_NAME_CHARACTERS.Matches(name);

                foreach (Match match in matches)
                    result += match.Value;

                displayName = result;

                if (!hasClaimedName && !string.IsNullOrEmpty(id) && id.Length > 4)
                    displayName = $"{result}{id}";

                UserNameColor = ProfileNameColorHelper.GetNameColor(displayName);
            }

            public override int GetHashCode() =>
                id.GetHashCode();

            private bool Equals(MemberData other) =>
                id.Equals(other.id);

            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MemberData) obj);
            }
        }

        public MemberData[] members;
        public int totalAmount;
    }
}


