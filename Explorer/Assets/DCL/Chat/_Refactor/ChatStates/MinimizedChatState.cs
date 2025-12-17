namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        public override void Enter()
        {
            context.UIMediator.SetupForMinimizedState();

            context.UIMediator.chatInputPresenter.OnMinimize();
        }

        public override void Exit()
        {

        }

        public override void OnFocusRequested() =>
            ChangeState<FocusedChatState>();

        /// <summary>
        ///     NOTE: If we are in the minimized state
        ///     NOTE: toggle to default state
        /// </summary>
        public override void OnMinimizeRequested()
        {
            ChangeState<FocusedChatState>();
        }
    }
}
