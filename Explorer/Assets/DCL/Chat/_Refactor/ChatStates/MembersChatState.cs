namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState
    {
        public override void Enter()
        {
            context.UIMediator.SetupForMembersState();
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
