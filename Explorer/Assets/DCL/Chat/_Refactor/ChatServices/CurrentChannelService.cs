using DCL.Chat.History;

namespace DCL.Chat.Services
{
    public interface ICurrentChannelService
    {
        ChatChannel CurrentChannel { get; }
        ChatChannel.ChannelId CurrentChannelId { get; }

        void SetCurrentChannel(ChatChannel newChannel);
        void Clear();
    }
    
    public class CurrentChannelService : ICurrentChannelService
    {
        public ChatChannel CurrentChannel { get; private set; }
        public ChatChannel.ChannelId CurrentChannelId => CurrentChannel?.Id ?? default;

        public void SetCurrentChannel(ChatChannel newChannel)
        {
            CurrentChannel = newChannel;
        }

        public void Clear()
        {
            CurrentChannel = null;
        }
    }
}