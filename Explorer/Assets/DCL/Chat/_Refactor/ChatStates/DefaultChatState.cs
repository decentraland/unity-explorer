namespace DCL.Chat.ChatStates
{
    /// <summary>
    ///     Blurred/Unfocused state of the chat.
    /// </summary>
    public class DefaultChatState : ChatState
    {
        public override void Begin()
        {
            context.UIMediator.SetupForDefaultState(animate: true);
            context.UIMediator.chatInputPresenter.OnBlur();
        }

        public override void OnPointerEnter() =>
            context.UIMediator.SetPanelsFocus(isFocused: true, animate: true);

        public override void OnPointerExit() =>
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
