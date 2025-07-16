namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        public override void Begin()
        {
            context.UIMediator.SetupForMinimizedState();

            context.UIMediator.chatInputPresenter.OnMinimize();
        }

        public override void End()
        {

        }

        public override void OnFocusRequested() =>
            ChangeState<FocusedChatState>();
    }
}
