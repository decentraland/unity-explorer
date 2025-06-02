using CommunicationData.URLHelpers;
using DCL.Friends;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Web3;

namespace DCL.Communities.CommunitiesCard
{
    public static class MemberExtensions
    {
        public static UserProfileContextMenuControlSettings.UserData ToUserData(this GetCommunityMembersResponse.MemberData profile) =>
            new ()
            {
                hasClaimedName = profile.hasClaimedName,
                userAddress = profile.id,
                userColor = profile.GetUserNameColor(),
                userName = profile.name,
                userThumbnailAddress = profile.profilePicture
            };

        public static GetCommunityMembersResponse.MemberData ToMemberData(this UserProfileContextMenuControlSettings.UserData user) =>
            new (user.userAddress, user.userThumbnailAddress, user.userName, user.hasClaimedName);

        public static FriendProfile ToFriendProfile(this UserProfileContextMenuControlSettings.UserData user) =>
            new (new Web3Address(user.userAddress), user.userName, user.hasClaimedName, URLAddress.FromString(user.userThumbnailAddress), user.userColor);

    }
}
