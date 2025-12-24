using MVC;

namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState
    {
        private readonly MVCStateMachine chatStateMachine;
        private readonly ChatUIMediator mediator;

        public MembersChatState(MVCStateMachine chatStateMachine, ChatUIMediator mediator)
        {
            this.chatStateMachine = chatStateMachine;
            this.mediator = mediator;
        }

        public override void Enter()
        {
            mediator.SetupForMembersState();
        }

        public override void Exit() { }

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
