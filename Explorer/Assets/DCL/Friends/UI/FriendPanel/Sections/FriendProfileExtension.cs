using DCL.NotificationsBus.NotificationTypes;
using DCL.Profiles;
using DCL.Utility.Types;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendProfileExtension
    {
        public static Option<Profile.CompactInfo> ToFriendProfile(this FriendRequestProfile profile)
        {
            Option<UserId> userId = UserId.New(profile.Address);

            return userId.Has
                ? Option<Profile.CompactInfo>.Some(new Profile.CompactInfo(userId.Value, profile.Name, profile.HasClaimedName, profile.ProfileImageUrl))
                : Option<Profile.CompactInfo>.None;
        }
    }
}
