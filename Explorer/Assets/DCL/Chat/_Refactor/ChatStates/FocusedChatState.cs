using DCL.Chat.EventBus;

namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState
    {
        public override void begin()
        {
            _context.Mediator.SetupForFocusedState();
            
            _context.InputBlocker.Block();

            _context.Mediator.chatInputPresenter.OnFocus();
        }

        public override void end()
        {
            _context.InputBlocker.Unblock();
        }
    }
}