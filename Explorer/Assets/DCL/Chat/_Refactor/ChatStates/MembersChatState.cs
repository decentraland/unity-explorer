using MVC;

namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState, IState
    {
        private readonly MVCStateMachine<ChatState> chatStateMachine;
        private readonly ChatUIMediator mediator;

        public MembersChatState(MVCStateMachine<ChatState> chatStateMachine, ChatUIMediator mediator)
        {
            this.chatStateMachine = chatStateMachine;
            this.mediator = mediator;
        }

        public void Enter()
        {
            mediator.SetupForMembersState();
        }

        public override void OnToggleMembers() =>
            chatStateMachine.Enter<FocusedChatState>();

        public override void OnFocusRequested() =>
            chatStateMachine.Enter<FocusedChatState>();

        public override void OnCloseRequested() =>
            chatStateMachine.Enter<FocusedChatState>();

        public override void OnClickOutside() =>
            chatStateMachine.Enter<DefaultChatState>();
    }
}
