using DCL.Chat.History;
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
        public Sprite? ProfilePicture { get; set; }

        public UserChannelViewModel(ChatChannel.ChannelId id)
            : base(id, ChatChannel.ChatChannelType.USER)
        {
            DisplayName = "Loading..."; // Initial state
        }
    }
}