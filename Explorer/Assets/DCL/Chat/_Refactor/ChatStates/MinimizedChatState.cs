namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        public override void begin()
        {
            _context.Mediator.SetupForMinimizedState();
        }

        public override void end()
        {
            
        }
    }
}