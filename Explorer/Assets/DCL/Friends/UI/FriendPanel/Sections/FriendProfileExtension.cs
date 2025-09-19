using CommunicationData.URLHelpers;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.UI.Controls.Configs;
using DCL.Web3;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendProfileExtension
    {
        public static FriendProfile ToFriendProfile(this Profile profile) =>
            new (new Web3Address(profile.UserId), profile.Name, profile.HasClaimedName, profile.Avatar.FaceSnapshotUrl, profile.UserNameColor);

        public static FriendProfile ToFriendProfile(this FriendRequestProfile profile) =>
            new (new Web3Address(profile.Address), profile.Name, profile.HasClaimedName, URLAddress.FromString(profile.ProfileImageUrl), ProfileNameColorHelper.GetNameColor(profile.Name));

        public static UserProfileContextMenuControlSettings.UserData ToUserData(this FriendProfile friendProfile) =>
            new ()
            {
                hasClaimedName = friendProfile.HasClaimedName,
                userAddress = friendProfile.Address,
                userColor = friendProfile.UserNameColor,
                userName = friendProfile.Name,
                userThumbnailAddress = friendProfile.FacePictureUrl
            };

        public static UserProfileContextMenuControlSettings.UserData ToUserData(this BlockedProfile blockedProfile) =>
            new ()
            {
                hasClaimedName = blockedProfile.HasClaimedName,
                userAddress = blockedProfile.Address,
                userColor = blockedProfile.UserNameColor,
                userName = blockedProfile.Name,
                userThumbnailAddress = blockedProfile.FacePictureUrl
            };

        public static UserProfileContextMenuControlSettings.UserData ToUserData(this Profile profile) =>
            new ()
            {
                hasClaimedName = profile.HasClaimedName,
                userAddress = profile.UserId,
                userColor = profile.UserNameColor,
                userName = profile.Name,
                userThumbnailAddress = profile.Avatar.FaceSnapshotUrl
            };
    }
}
