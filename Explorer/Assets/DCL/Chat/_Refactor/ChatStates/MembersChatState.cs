namespace DCL.Chat.ChatStates
{
    public class MembersChatState : ChatState
    {
        public override void Begin()
        {
            context.UIMediator.SetupForMembersState();
        }

        public override void End() { }

        public override void OnToggleMembers() =>
            ChangeState<FocusedChatState>();

        public override void OnCloseRequested() =>
            ChangeState<FocusedChatState>();
    }
}
