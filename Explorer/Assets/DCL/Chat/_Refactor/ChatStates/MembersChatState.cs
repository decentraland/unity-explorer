namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState
    {
        public override void begin()
        {
            _context.Mediator.SetupForMembersState();
        }

        public override void end()
        {
            
        }
    }
}