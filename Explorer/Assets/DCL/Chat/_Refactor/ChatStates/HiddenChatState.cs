using System;

namespace DCL.Chat.ChatStates
{
    public class HiddenChatState : ChatState
    {
        private readonly ChatUIMediator mediator;

        public HiddenChatState(ChatUIMediator mediator)
        {
            this.mediator = mediator;
        }

        public override void Enter()
        {
            mediator.SetupForHiddenState();
        }

        public override void Exit()
        {

        }
    }
}
