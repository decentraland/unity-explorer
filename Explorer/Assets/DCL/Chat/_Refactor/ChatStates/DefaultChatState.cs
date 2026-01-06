using MVC;

namespace DCL.Chat.ChatStates
{
    /// <summary>
    ///     Blurred/Unfocused state of the chat.
    /// </summary>
    public class DefaultChatState : ChatState, IState
    {
        private readonly MVCStateMachine<ChatState> chatStateMachine;
        private readonly ChatUIMediator uiMediator;

        public DefaultChatState(MVCStateMachine<ChatState> chatStateMachine, ChatUIMediator uiMediator)
        {
            this.chatStateMachine = chatStateMachine;
            this.uiMediator = uiMediator;
        }

        public void Enter()
        {
            uiMediator.SetupForDefaultState(animate: true);
            uiMediator.chatInputPresenter.OnBlur();
        }

        public override void OnPointerEnter() =>
            uiMediator.SetPanelsFocus(isFocused: true, animate: true);

        public override void OnPointerExit() =>
            uiMediator.SetPanelsFocus(isFocused: false, animate: true);

        public override void OnClickInside() =>
            chatStateMachine.Enter<FocusedChatState>();

        public override void OnCloseRequested() =>
            chatStateMachine.Enter<MinimizedChatState>();

        public override void OnFocusRequested() =>
            chatStateMachine.Enter<FocusedChatState>();

        public override void OnMinimizeRequested() =>
            chatStateMachine.Enter<MinimizedChatState>();

        public override void OnToggleMembers() =>
            chatStateMachine.Enter<MembersChatState>();
    }
}
