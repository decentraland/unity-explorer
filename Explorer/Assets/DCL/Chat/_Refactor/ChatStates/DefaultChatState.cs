using DCL.Chat.ChatServices;

namespace DCL.Chat.ChatStates
{
    /// <summary>
    ///     Blurred/Unfocused state of the chat.
    /// </summary>
    public class DefaultChatState : ChatState
    {
        private readonly ChatUIMediator uiMediator;

        public DefaultChatState(ChatUIMediator uiMediator)
        {
            this.uiMediator = uiMediator;
        }

        public override void Enter()
        {
            uiMediator.SetupForDefaultState(animate: true);
            uiMediator.chatInputPresenter.OnBlur();
        }

        public override void OnPointerEnter() =>
            uiMediator.SetPanelsFocus(isFocused: true, animate: true);

        public override void OnPointerExit() =>
            uiMediator.SetPanelsFocus(isFocused: false, animate: true);

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
