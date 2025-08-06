using Utility;

namespace DCL.Chat.ChatCommands
{
    public class ResetChatCommand
    {
        private readonly IEventBus eventBus;

        public ResetChatCommand(IEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void Execute()
        {
            eventBus.Publish(new ChatEvents.ChatResetEvent());
        }
    }
}