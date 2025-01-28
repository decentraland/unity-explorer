using DCL.Profiles;
using DCL.Web3;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public static class FriendProfileExtension
    {
        public static FriendProfile ToFriendProfile(this Profile profile) =>
            new (new Web3Address(profile.UserId), profile.Name, profile.HasClaimedName, profile.Avatar.FaceSnapshotUrl);
    }
}
