namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState,
        IToggleMembersHandler,
        ICloseRequestHandler
    {
        public override void begin()
        {
            _context.Mediator.SetupForMembersState();
        }

        public override void end()
        {
            
        }
        
        public void OnToggleMembers() => _machine.changeState<FocusedChatState>();

        public void OnCloseRequested() => _machine.changeState<FocusedChatState>();
    }
}