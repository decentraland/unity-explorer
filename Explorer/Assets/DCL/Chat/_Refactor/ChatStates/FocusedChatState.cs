namespace DCL.Chat.ChatStates
{
    public class FocusedChatState : ChatState, 
        IClickOutsideHandler,
        ICloseRequestHandler,
        IToggleMembersHandler,
        IMinimizeRequestHandler
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
        
        public void OnClickOutside() => _machine.changeState<DefaultChatState>();
        public void OnCloseRequested() => _machine.changeState<MinimizedChatState>();
        public void OnMinimizeRequested() => _machine.changeState<MinimizedChatState>();
        public void OnToggleMembers() => _machine.changeState<MembersChatState>();
    }
}