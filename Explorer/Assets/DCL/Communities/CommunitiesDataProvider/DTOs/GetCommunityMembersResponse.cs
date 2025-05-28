
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Utility;
using System;

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

            private const string HEX_CHARS = "0123456789abcdef";
            private static readonly string[] ADJECTIVES =
            {
                "cool", "fast", "silent", "happy", "dark", "bright",
                "blue", "frozen", "angry", "brave", "smart", "wild"
            };

            private static readonly string[] NOUNS =
            {
                "fox", "wolf", "rider", "ghost", "cat", "hawk", "stone",
                "blade", "shadow", "storm", "dragon", "raven"
            };

            private static readonly CommunityMemberRole[] ROLES = EnumUtils.Values<CommunityMemberRole>();
            private static readonly FriendshipStatus[] FRIENDSHIP_STATUSES = EnumUtils.Values<FriendshipStatus>();

            public static MemberData RandomMember()
            {
                var sb = new StringBuilder("0x");

                for (int i = 0; i < 40; i++)
                    sb.Append(HEX_CHARS[UnityEngine.Random.Range(0, HEX_CHARS.Length)]);

                return new MemberData(sb.ToString(),
                    "",
                    $"{ADJECTIVES[UnityEngine.Random.Range(0, ADJECTIVES.Length)]}{NOUNS[UnityEngine.Random.Range(0, NOUNS.Length)]}",
                    UnityEngine.Random.Range(0, 100) > 50,
                    ROLES[UnityEngine.Random.Range(0, ROLES.Length)],
                    UnityEngine.Random.Range(0, 10),
                    FRIENDSHIP_STATUSES[UnityEngine.Random.Range(0, FRIENDSHIP_STATUSES.Length)]);
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


