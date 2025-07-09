using DCL.Diagnostics;

namespace DCL.Chat.ChatStates
{
    /// <summary>
    /// Purpose: Chat is "folded", only the input box is visible.
    /// begin() (Entry Actions):
    ///     UI: Tell the view to hide the main panels (Title, Messages, Conversations Toolbar). view.SetMainPanelsVisibility(false);
    ///     Subscribe: _context.chatInputPresenter.OnInputClicked += GoToDefaultState;
    /// end() (Exit Actions):
    ///     NOTE: this might be done in the default state
    ///     UI: Tell the view to show the main panels. view.SetMainPanelsVisibility(true);
    ///     Unsubscribe: _context.chatInputPresenter.OnInputClicked -= GoToDefaultState;
    /// Event Handler:
    ///     GoToDefaultState() -> _machine.changeState<DefaultChatState>();
    /// </summary>
    public class MinimizedChatState : ChatState
    {
        public override void begin()
        {
            _context.titleBarPresenter?.Hide();
            _context.chatChannelsPresenter?.Hide();
            _context.messageViewerPresenter?.Hide();
            _context.memberListPresenter?.Deactivate();
            _context.chatInputPresenter?.Show();
            

            // --- Input ---
            //_context.UnblockPlayerInput();
        }

        public override void end()
        {
            
        }
    }
}