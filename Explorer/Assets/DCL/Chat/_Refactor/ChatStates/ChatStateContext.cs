using DCL.Chat.ChatServices;

namespace DCL.Chat.ChatStates
{
    public readonly struct ChatStateContext
    {
        public readonly ChatUIMediator UIMediator;
        public readonly ChatInputBlockingService InputBlocker;

        public ChatStateContext(ChatUIMediator uiMediator, ChatInputBlockingService inputBlocker)
        {
            UIMediator = uiMediator;
            InputBlocker = inputBlocker;
        }
    }
}
