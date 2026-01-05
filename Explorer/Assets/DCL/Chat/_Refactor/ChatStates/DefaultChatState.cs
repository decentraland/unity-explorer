namespace DCL.Chat.ChatStates
{
    /// <summary>
    ///     Blurred/Unfocused state of the chat.
    /// </summary>
    public class DefaultChatState : ChatState
    {
        public override void Enter()
        {
            context.UIMediator.SetupForDefaultState(animate: true);
            context.UIMediator.chatInputPresenter.OnBlur();
        }

        public override void OnPointerEnter() =>
            context.UIMediator.SetPanelsFocus(isFocused: true, animate: true);

        public override void OnPointerExit() =>
            context.UIMediator.SetPanelsFocus(isFocused: false, animate: true);

        public override void OnClickInside() =>
            machine.Enter<FocusedChatState>();

        public override void OnCloseRequested() =>
            machine.Enter<MinimizedChatState>();

        public override void OnFocusRequested() =>
            machine.Enter<FocusedChatState>();

        public override void OnMinimizeRequested() =>
            machine.Enter<MinimizedChatState>();

        public override void OnToggleMembers() =>
            machine.Enter<MembersChatState>();
    }
}
