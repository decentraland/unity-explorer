using DCL.Diagnostics;

namespace DCL.Chat.ChatStates
{
    /// <summary>
    /// Purpose: User is actively typing. The panel is fully visible and player input is blocked.
    /// begin() (Entry Actions):
    ///     Input: Block player movement. _context.inputBlock.Disable(...)
    ///     UI: Ensure the view is fully "unblurred". view.SetBlurred(false, isInstant: true);
    ///     UI: Tell the input presenter to focus itself. _context.chatInputPresenter.Focus();
    ///     Subscribe:
    ///         _context.viewInstance.ClickedOutside += GoToDefaultState;
    ///         DCLInput.Instance.UI.Close.performed += OnEscapePressed;
    /// end() (Exit Actions):
    ///     Input: Re-enable player movement. _context.inputBlock.Enable(...)
    ///     Unsubscribe: from all events subscribed in begin().
    /// Event Handlers:
    ///     GoToDefaultState() -> _machine.changeState<DefaultChatState>();
    ///     OnEscapePressed(...) -> _machine.changeState<DefaultChatState>();
    /// </summary>
    public class FocusedChatState : ChatState
    {
        public override void begin()
        {
            // block player movement
            _context.memberListPresenter?.Deactivate();
            _context.titleBarPresenter?.Show();
            _context.chatChannelsPresenter?.Show();
            _context.messageViewerPresenter?.Show();
            _context.chatInputPresenter?.Show();
            
            // clicked outside goes into default state
        }

        public override void end()
        {
            ReportHub.Log(ReportCategory.UNSPECIFIED, "[ChatState] FocusedChatState: begin");
        }
    }
}