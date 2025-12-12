using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendProfileExtension
    {
        public static Profile.CompactInfo ToFriendProfile(this FriendRequestProfile profile) =>
            new (profile.Address, profile.Name, profile.HasClaimedName, profile.ProfileImageUrl);
    }
}
