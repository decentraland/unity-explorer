namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        public override void Begin()
        {
            context.UIMediator.SetupForMinimizedState();
            context.UIMediator.chatInputPresenter.OnMinimize();
        }

        public override void End() { }

        public override void OnFocusRequested() =>
            ChangeState<FocusedChatState>();

        /// <summary>
        ///     NOTE: If we are in the minimized state
        ///     NOTE: toggle to default state
        /// </summary>
        public override void OnMinimizeRequested()
        {
            //TODO FRAN: IF we are already minimized, we don't do anything.
            //ChangeState<FocusedChatState>();
        }
    }
}
