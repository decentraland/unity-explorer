using DCL.Chat.ChatServices;
using DCL.Chat.History;

namespace DCL.Chat.ChatCommands
{
    public class DeleteChatHistoryCommand
    {
        private readonly ChatEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly CurrentChannelService currentChannelService;

        public DeleteChatHistoryCommand(
            ChatEventBus eventBus,
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

            if (chatHistory.Channels.ContainsKey(channelId))
            {
                chatHistory.ClearChannel(channelId);

                eventBus.RaiseChatHistoryClearedEvent(channelId);
            }
        }
    }
}
