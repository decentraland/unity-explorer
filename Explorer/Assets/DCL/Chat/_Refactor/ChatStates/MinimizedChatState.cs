using DCL.Diagnostics;

namespace DCL.Chat.ChatStates
{
    public class MinimizedChatState : ChatState
    {
        public override void begin()
        {
            _context.titleBarPresenter?.Hide();
            _context.chatChannelsPresenter?.Hide();
            _context.messageViewerPresenter?.Hide();
            _context.memberListPresenter?.Deactivate();
            _context.chatInputPresenter?.Show();
            _context.chatInputPresenter?.SetInactiveMode();
            _context.chatInputPresenter.OnFocusRequested += GoToFocusedState;

            _context.SetPanelsFocusState(isFocused: false, animate: true);
            // --- Input ---
            //_context.UnblockPlayerInput();
        }

        public override void end()
        {
            _context.chatInputPresenter.OnFocusRequested -= GoToFocusedState;
        }
        
        private void GoToFocusedState() => _machine.changeState<FocusedChatState>();
    }
}