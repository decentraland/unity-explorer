namespace DCL.Chat.ChatStates
{
    public class DefaultChatState : ChatState, 
        IClickInsideHandler,
        ICloseRequestHandler,
        IFocusRequestHandler,
        IMinimizeRequestHandler,
        IToggleMembersHandler
    {
        public override void begin()
        {
            _context.Mediator.SetupForDefaultState(animate: true);
            _context.Mediator.chatInputPresenter.OnDefocus();
            
            _context.MainController.PointerEntered += OnPointerEnter;
            _context.MainController.PointerExited += OnPointerExit;
        }

        public override void end()
        {
            _context.MainController.PointerEntered -= OnPointerEnter;
            _context.MainController.PointerExited -= OnPointerExit;
        }

        private void OnPointerEnter() => _context.Mediator.SetPanelsFocus(isFocused: true, animate: true);
        private void OnPointerExit() => _context.Mediator.SetPanelsFocus(isFocused: false, animate: true);
        public void OnClickInside() => _machine.changeState<FocusedChatState>();
        public void OnCloseRequested() => _machine.changeState<MinimizedChatState>();
        public void OnFocusRequested() => _machine.changeState<FocusedChatState>();
        public void OnMinimizeRequested() => _machine.changeState<MinimizedChatState>();
        public void OnToggleMembers() => _machine.changeState<MembersChatState>();
    }
}