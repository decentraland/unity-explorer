using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMemberListViewModel
    {
        public Profile.CompactInfo Profile;
        public readonly bool IsOnline;
        public readonly IReactiveProperty<ProfileThumbnailViewModel> ProfileThumbnail;

        public string UserName => Profile.ValidatedName;

        public ChatMemberListViewModel(Profile.CompactInfo profile, bool isOnline)
        {
            Profile = profile;
            IsOnline = isOnline;

            ProfileThumbnail = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default(profile.UserNameColor));
        }
    }
}
