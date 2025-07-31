using DCL.Chat.ChatServices;
using DCL.Chat.History;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class DeleteChatHistoryCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly CurrentChannelService currentChannelService;

        public DeleteChatHistoryCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
        }

        public void Execute()
        {
            var channelId = currentChannelService.CurrentChannelId;

            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID)) return;

            if (chatHistory.Channels.ContainsKey(channelId))
            {
                chatHistory.ClearChannel(channelId);

                eventBus.Publish(new ChatEvents.ChatHistoryClearedEvent
                {
                    ChannelId = channelId
                });
            }
        }
    }
}
