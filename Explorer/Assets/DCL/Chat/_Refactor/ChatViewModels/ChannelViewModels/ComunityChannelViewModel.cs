using DCL.Chat.History;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class CommunityChannelViewModel : BaseChannelViewModel
    {
        public string DisplayName { get; set; }
        public string ImageUrl { get; set; }
        public Sprite? Thumbnail { get; set; }

        public CommunityChannelViewModel(ChatChannel.ChannelId id, int unreadMessagesCount, bool hasUnreadMentions)
            : base(id, ChatChannel.ChatChannelType.COMMUNITY, unreadMessagesCount, hasUnreadMentions)
        {
            DisplayName = "Loading...";
        }
    }
}