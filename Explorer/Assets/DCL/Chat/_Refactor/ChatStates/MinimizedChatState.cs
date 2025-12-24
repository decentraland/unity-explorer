namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        private readonly ChatUIMediator mediator;

        public MinimizedChatState(ChatUIMediator mediator)
        {
            this.mediator = mediator;
        }

        public override void Enter()
        {
            mediator.SetupForMinimizedState();
            mediator.chatInputPresenter.OnMinimize();
        }

        public override void Exit() { }

        public override void OnFocusRequested() =>
            machine.Enter<FocusedChatState>();

        /// <summary>
        ///     NOTE: If we are in the minimized state
        ///     NOTE: toggle to default state
        /// </summary>
        public override void OnMinimizeRequested() =>
            machine.Enter<FocusedChatState>();
    }
}
