using DCL.Diagnostics;

namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState
    {
        public override void begin()
        {
            _context.Mediator.SetupForFocusedState();
            _context.InputBlocker.Block();
        }

        public override void end()
        {
            _context.InputBlocker.Unblock();
        }
    }
}