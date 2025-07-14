namespace DCL.Chat._Refactor.ChatStates
{
    public class HiddenChatState : ChatState
    {
        public override void begin()
        {
            _context.Mediator.SetupForHiddenState();
        }

        public override void end()
        {
            
        }
    }
}