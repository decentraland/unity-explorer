using DCL.Chat.ChatServices;
using DCL.Chat.History;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class ResetChatCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly CurrentChannelService currentChannelService;

        public ResetChatCommand(IEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void Execute()
        {
            eventBus.Publish(new ChatEvents.ChatResetEvent());

            chatHistory.DeleteAllChannels();
            currentChannelService.Dispose();
            
        }
    }
}