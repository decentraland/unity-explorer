using DCL.Profiles;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    // Server schema: social-service-ea docs/schemas.yaml#/components/schemas/GetCommunityMembersV2200OkResponse (also reused for GetBannedMembersV2200OkResponse).
    [Serializable]
    public class GetCommunityMembersResponse : ICommunityMemberPagedResponse
    {
        [Serializable]
        public class MemberData : ICommunityMemberData
        {
            // Hydrated client-side from memberAddress; the server no longer sends inner profile info.
            [JsonIgnore]
            public Profile.CompactInfo Profile { get; internal set; }

            public string memberAddress = null!;
            public string communityId = null!;
            public CommunityMemberRole role;
            public string joinedAt = null!;
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

            public string Id => communityId;
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

            public Color GetUserNameColor() =>
                Profile.UserNameColor;

            public override int GetHashCode() =>
                Profile.UserId.GetHashCode();

            private bool Equals(MemberData other) =>
                Profile.UserId.Equals(other.Profile.UserId);

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
            public MemberData[] results = Array.Empty<MemberData>();
            public int total;
            public int page;
            public int pages;
            public int limit;
        }

        public GetCommunityMembersResponseData data = null!;
        public ICommunityMemberData[] members => data.results;
        public int total => data.total;
    }
}


