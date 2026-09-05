using DCL.Chat.History;

namespace DCL.Chat.ChatViewModels
{
    public abstract class BaseChannelViewModel
    {
        public ChatChannel.ChannelId Id { get; }
        public ChatChannel.ChatChannelType ChannelType { get; }
        public int UnreadMessagesCount { get; set; }
        public bool HasUnreadMentions { get; set; }
        public bool IsSelected { get; set; }

        protected BaseChannelViewModel(ChatChannel.ChannelId id,
            ChatChannel.ChatChannelType channelType,
            int unreadMessagesCount = 0,
            bool hasUnreadMentions = false)
        {
            Id = id;
            ChannelType = channelType;
            UnreadMessagesCount = unreadMessagesCount;
            HasUnreadMentions = hasUnreadMentions;
        }
    }
}