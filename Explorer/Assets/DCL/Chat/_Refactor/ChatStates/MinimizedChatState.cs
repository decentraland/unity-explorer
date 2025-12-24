using MVC;

namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        private readonly MVCStateMachine stateMachine;
        private readonly ChatUIMediator mediator;

        public MinimizedChatState(MVCStateMachine stateMachine, ChatUIMediator mediator)
        {
            this.stateMachine = stateMachine;
            this.mediator = mediator;
        }

        public override void Enter()
        {
            mediator.SetupForMinimizedState();
            mediator.chatInputPresenter.OnMinimize();
        }

        public override void Exit() { }

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
