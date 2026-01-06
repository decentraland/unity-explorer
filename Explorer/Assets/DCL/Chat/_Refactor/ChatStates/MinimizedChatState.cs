using MVC;

namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState, IState
    {
        private readonly MVCStateMachine<ChatState> stateMachine;
        private readonly ChatUIMediator mediator;

        public MinimizedChatState(MVCStateMachine<ChatState> stateMachine, ChatUIMediator mediator)
        {
            this.stateMachine = stateMachine;
            this.mediator = mediator;
        }

        public void Enter()
        {
            mediator.SetupForMinimizedState();
            mediator.chatInputPresenter.OnMinimize();
        }

        public override void OnFocusRequested() =>
            stateMachine.Enter<FocusedChatState>();

        /// <summary>
        ///     NOTE: If we are in the minimized state
        ///     NOTE: toggle to default state
        /// </summary>
        public override void OnMinimizeRequested() =>
            stateMachine.Enter<FocusedChatState>();
    }
}
