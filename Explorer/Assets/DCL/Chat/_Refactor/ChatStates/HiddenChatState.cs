using MVC;

namespace DCL.Chat.ChatStates
{
    public class HiddenChatState : ChatState, IState
    {
        private readonly ChatUIMediator mediator;

        public HiddenChatState(ChatUIMediator mediator)
        {
            this.mediator = mediator;
        }

        public void Enter()
        {
            mediator.SetupForHiddenState();
        }
    }
}
