using DCL.Diagnostics;
using UnityEngine;

namespace DCL.Chat.ChatStates
{
    /// <summary>
    /// Purpose: Chat is "unfolded", but not focused. Handles the blur/unblur visual effect on hover. Player can still move.
    /// begin() (Entry Actions):
    ///     UI: Ensure all main panels are visible.
    ///     UI: Tell the view to enter the "blurred" state initially. view.SetBlurred(true, isInstant: true);
    ///     Subscribe:
    ///         _context.viewInstance.PointerEntered += OnPointerEnter;
    ///         _context.viewInstance.PointerExited += OnPointerExit;
    ///         _context.chatInputPresenter.OnInputClicked += GoToFocusedState;
    ///         _context.titleBarPresenter.OnCloseChat += GoToMinimizedState; // From "Close" button
    /// end() (Exit Actions):
    ///     UI: Ensure the view is fully "unblurred" before leaving. view.SetBlurred(false, isInstant: true);
    ///     Unsubscribe: from all events subscribed in begin().
    ///     Event Handlers:
    ///         OnPointerEnter() -> _context.viewInstance.SetBlurred(false, isInstant: false);
    ///         OnPointerExit() -> _context.viewInstance.SetBlurred(true, isInstant: false);
    ///         GoToFocusedState() -> _machine.changeState<FocusedChatState>();
    ///         GoToMinimizedState() -> _machine.changeState<MinimizedChatState>();
    /// </summary>
    public class DefaultChatState : ChatState
    {
        // NOTE: there is a lot of overlap with the FocusedChatState, but this is the default state
        public override void begin()
        {
            // TODO: background
            _context.memberListPresenter?.Deactivate();
            _context.titleBarPresenter?.Hide();
            _context.chatChannelsPresenter?.Hide();
            _context.messageViewerPresenter?.Show();
            _context.chatInputPresenter?.Show();
            
            _context.OnPointerEnter += OnPointerEnter;
            _context.OnPointerExit += OnPointerExit;

            if (_context.titleBarPresenter != null) 
                _context.titleBarPresenter.OnMemberListToggle += OnMemberListToggled;
        }

        public override void end()
        {
            // unsubscribe all
            _context.OnPointerEnter -= OnPointerEnter;
            _context.OnPointerExit -= OnPointerExit;

            if (_context.titleBarPresenter != null) 
                _context.titleBarPresenter.OnMemberListToggle -= OnMemberListToggled;
        }
        
        private void OnMemberListToggled(bool memberListVisible)
        {
            if (memberListVisible)
                _machine.changeState<MembersChatState>();
        }
        
        private void OnCloseChat()
        {
            _machine.changeState<MinimizedChatState>();
        }
        
        private void OnPointerEnter()
        {
            _context.titleBarPresenter?.Show();
            _context.chatChannelsPresenter?.Show();
        }
        
        private void OnPointerExit()
        {
            _context.titleBarPresenter?.Hide();
            _context.chatChannelsPresenter?.Hide();
        }
    }
}