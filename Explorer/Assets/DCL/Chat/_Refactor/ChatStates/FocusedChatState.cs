using DCL.Diagnostics;

namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState
    {
        public override void begin()
        {
            _context.memberListPresenter?.Deactivate();
            _context.titleBarPresenter?.Show();
            _context.chatChannelsPresenter?.Show();
            _context.messageViewerPresenter?.Show();
            _context.chatInputPresenter?.Show();
            _context.chatInputPresenter.SetActiveMode();
            
            _context.SetPanelsFocusState(isFocused: true, animate: false);
            _context.BlockPlayerInput();
            _context.OnClickOutside += GoToDefaultState;
        }

        public override void end()
        {
            _context.UnblockPlayerInput();
            _context.OnClickOutside -= GoToDefaultState;
        }

        private void GoToDefaultState()
        {
            // TODO: check if any of the popups are open, if so, don't change state
            // TODO: this applies to context menu, emoji panel, etc.
            // TODO: any popup should handle it's own closing when clicking outside of it
            _machine.changeState<DefaultChatState>();
        }
    }
}