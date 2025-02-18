using CommunicationData.URLHelpers;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendProfileExtension
    {
        public static FriendProfile ToFriendProfile(this Profile profile) =>
            new (new Web3Address(profile.UserId), profile.Name, profile.HasClaimedName, profile.Avatar.FaceSnapshotUrl, profile.UserNameColor);

        public static FriendProfile ToFriendProfile(this FriendRequestProfile profile, IProfileNameColorHelper profileNameColorHelper) =>
            new (new Web3Address(profile.Address), profile.Name, profile.HasClaimedName, URLAddress.FromString(profile.ProfileImageUrl), profileNameColorHelper.GetNameColor(profile.Name));
    }
}
