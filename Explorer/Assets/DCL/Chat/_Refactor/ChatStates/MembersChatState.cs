namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState
    {
        private readonly ChatUIMediator mediator;

        public MembersChatState(ChatUIMediator mediator)
        {
            this.mediator = mediator;
        }

        public override void Enter()
        {
            mediator.SetupForMembersState();
        }

        public override void Exit() { }

        public override void OnToggleMembers() =>
            machine.Enter<FocusedChatState>();

        public override void OnFocusRequested() =>
            machine.Enter<FocusedChatState>();

        public override void OnCloseRequested() =>
            machine.Enter<FocusedChatState>();

        public override void OnClickOutside() =>
            machine.Enter<DefaultChatState>();
    }
}
