using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels.ChannelViewModels
{
    public class UserChannelViewModel : BaseChannelViewModel
    {
        public string DisplayName { get; set; }
        public bool IsOnline { get; set; }
        public bool HasClaimedName { get; set; }
        public IReactiveProperty<ProfileThumbnailViewModel.WithColor> ProfilePicture { get; } = ProfileThumbnailViewModel.WithColor.DefaultReactive();

        public UserChannelViewModel(ChatChannel.ChannelId id)
            : base(id, ChatChannel.ChatChannelType.USER)
        {
            DisplayName = "Loading..."; // Initial state
        }
    }
}
