using UnityEngine;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    public interface ICommunityMemberData
    {
        string Id { get; }
        string Address { get; }
        string ProfilePictureUrl { get; }
        bool HasClaimedName { get; }
        string Name { get; }
        int MutualFriends { get; }
        CommunityMemberRole Role { get; set; }
        FriendshipStatus FriendshipStatus { get; set; }
        Color GetUserNameColor();
    }
}
