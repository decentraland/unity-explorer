namespace DCL.Chat.ChatStates
{
    public class DefaultChatState : ChatState
    {
        // NOTE: there is a lot of overlap with the FocusedChatState, but this is the default state
        public override void begin()
        {
            _context.titleBarPresenter?.Show();
            _context.chatChannelsPresenter?.Show();
            _context.messageViewerPresenter?.Show();
            _context.chatInputPresenter?.Show();
            _context.chatInputPresenter.SetInactiveMode();
            
            _context.memberListPresenter?.Deactivate();

            _context.SetPanelsFocusState(isFocused: false, animate: true);

            //_context.UnblockPlayerInput();

            _context.chatInputPresenter.OnFocusRequested += GoToFocusedState;
            
            _context.OnPointerEnter += OnPointerEnter;
            _context.OnPointerExit += OnPointerExit;
            
            _context.OnClickInside += GoToFocusedState;

            if (_context.titleBarPresenter != null)
            {
                _context.titleBarPresenter.OnMemberListToggle += OnMemberListToggled;
                _context.titleBarPresenter.OnCloseChat += OnCloseChat;
            }
        }

        public override void end()
        {
            // unsubscribe all
            _context.OnPointerEnter -= OnPointerEnter;
            _context.OnPointerExit -= OnPointerExit;
            _context.OnClickInside -= GoToFocusedState;

            _context.chatInputPresenter.OnFocusRequested -= GoToFocusedState;
            
            if (_context.titleBarPresenter != null) 
                _context.titleBarPresenter.OnMemberListToggle -= OnMemberListToggled;
        }

        private void GoToFocusedState()
        {
            _machine.changeState<FocusedChatState>();
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
            _context.SetPanelsFocusState(isFocused: true, animate: true);
        }
        
        private void OnPointerExit()
        {
            _context.SetPanelsFocusState(isFocused: false, animate: true);
        }
    }
}