namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        public override void begin()
        {
            _context.Mediator.SetupForMinimizedState();
            
            _context.Mediator.chatInputPresenter.OnMinimize();
        }

        public override void end()
        {
            
        }
    }
}