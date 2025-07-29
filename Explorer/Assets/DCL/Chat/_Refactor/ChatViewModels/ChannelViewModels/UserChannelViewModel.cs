using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels.ChannelViewModels
{
    public class UserChannelViewModel : BaseChannelViewModel
    {
        public string DisplayName { get; set; }
        public string ImageUrl { get; set; } // URL for the profile picture
        public bool IsOnline { get; set; }
        public Color ProfileColor { get; set; }
        public bool HasClaimedName { get; set; }
        public IReactiveProperty<ProfileThumbnailViewModel> ProfilePicture { get; } = new ReactiveProperty<ProfileThumbnailViewModel>(ProfileThumbnailViewModel.Default());

        public UserChannelViewModel(ChatChannel.ChannelId id)
            : base(id, ChatChannel.ChatChannelType.USER)
        {
            DisplayName = "Loading..."; // Initial state
        }
    }
}
