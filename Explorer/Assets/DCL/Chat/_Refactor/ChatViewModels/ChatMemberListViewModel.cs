using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMemberListViewModel
    {
        public Profile.CompactInfo Profile;
        public readonly bool IsOnline;
        public readonly bool HasProfile;
        public readonly IReactiveProperty<ProfileThumbnailViewModel> ProfileThumbnail;

        public string UserName => Profile.ValidatedName;

        public ChatMemberListViewModel(Profile.CompactInfo profile, bool isOnline, bool hasProfile)
        {
            Profile = profile;
            IsOnline = isOnline;
            HasProfile = hasProfile;

            ProfileThumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default(profile.UserNameColor));
        }
    }
}
