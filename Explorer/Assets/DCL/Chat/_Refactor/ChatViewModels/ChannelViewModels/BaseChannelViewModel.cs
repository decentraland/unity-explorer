using DCL.Chat.History;

namespace DCL.Chat.ChatViewModels.ChannelViewModels
{
    public abstract class BaseChannelViewModel
    {
        public ChatChannel.ChannelId Id { get; }
        public ChatChannel.ChatChannelType ChannelType { get; }
        public int UnreadMessagesCount { get; set; }
        public bool IsSelected { get; set; }

        protected BaseChannelViewModel(ChatChannel.ChannelId id, ChatChannel.ChatChannelType channelType)
        {
            Id = id;
            ChannelType = channelType;
        }
    }
}