using DCL.Chat.ChatServices;
using DCL.Chat.History;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class SelectChannelCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly CurrentChannelService currentChannelService;

        public SelectChannelCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
        }

        public void Execute(ChatChannel.ChannelId channelId)
        {
            if (currentChannelService.CurrentChannelId.Equals(channelId)) { return; }

            if (chatHistory.Channels.TryGetValue(channelId, out ChatChannel? channel))
            {
                currentChannelService.SetCurrentChannel(channel);

                eventBus.Publish(new ChatEvents.ChannelSelectedEvent { Channel = channel });
            }

            // If the channel doesn't exist, we simply do nothing.
            // We could also log an error here if this case is unexpected.
        }
    }
}
