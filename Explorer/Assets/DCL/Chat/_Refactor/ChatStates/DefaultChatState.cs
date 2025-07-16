namespace DCL.Chat.ChatStates
{
    public class DefaultChatState : ChatState
    {
        public override void Begin()
        {
            context.UIMediator.SetupForDefaultState(animate: true);
            context.UIMediator.chatInputPresenter.OnDefocus();

            // TODO Propagate events via a View Bus to prevent a circular dependency on the main chat controller
            // Or make them active similar to other callbacks
            // _context.MainController.PointerEntered += OnPointerEnter;
            // _context.MainController.PointerExited += OnPointerExit;
        }

        private void OnPointerEnter() =>
            context.UIMediator.SetPanelsFocus(isFocused: true, animate: true);

        private void OnPointerExit() =>
            context.UIMediator.SetPanelsFocus(isFocused: false, animate: true);

        public override void OnClickInside() =>
            ChangeState<FocusedChatState>();

        public override void OnCloseRequested() =>
            ChangeState<MinimizedChatState>();

        public override void OnFocusRequested() =>
            ChangeState<FocusedChatState>();

        public override void OnMinimizeRequested() =>
            ChangeState<MinimizedChatState>();

        public override void OnToggleMembers() =>
            ChangeState<MembersChatState>();
    }
}
