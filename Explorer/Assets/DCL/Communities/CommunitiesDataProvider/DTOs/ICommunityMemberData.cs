using CommunicationData.URLHelpers;
using DCL.Profiles;
using UnityEngine;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    public interface ICommunityMemberData
    {
        string Id { get; }

        public Profile.CompactInfo Profile { get; }

        public string Address => Profile.UserId;
        public string ProfilePictureUrl => Profile.FaceSnapshotUrl;
        public bool HasClaimedName => Profile.HasClaimedName;
        public string Name => Profile.Name;

        int MutualFriends { get; }
        CommunityMemberRole Role { get; set; }
        FriendshipStatus FriendshipStatus { get; set; }

        public Color GetUserNameColor() =>
            Profile.UserNameColor;
    }
}
