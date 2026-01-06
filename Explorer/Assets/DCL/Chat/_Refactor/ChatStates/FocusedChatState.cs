namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState
    {
        public override void Enter()
        {
            context.UIMediator.SetupForFocusedState();

            context.InputBlocker.Block();
        }

        public override void Exit()
        {
            context.InputBlocker.Unblock();
        }

        public override void OnClickOutside() =>
            machine.Enter<DefaultChatState>();

        public override void OnCloseRequested() =>
            machine.Enter<MinimizedChatState>();

        public override void OnMinimizeRequested() =>
            machine.Enter<MinimizedChatState>();

        public override void OnToggleMembers() =>
            machine.Enter<MembersChatState>();
    }
}
