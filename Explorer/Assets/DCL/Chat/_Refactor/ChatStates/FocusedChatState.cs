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
            ChangeState<DefaultChatState>();

        public override void OnCloseRequested() =>
            ChangeState<MinimizedChatState>();

        public override void OnMinimizeRequested() =>
            ChangeState<MinimizedChatState>();

        public override void OnToggleMembers() =>
            ChangeState<MembersChatState>();
    }
}
