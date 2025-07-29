using DCL.Chat.ChatMediator;
using DCL.Chat.ChatServices;

namespace DCL.Chat._Refactor.ChatStates
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
