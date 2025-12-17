namespace DCL.Chat.ChatStates
{
    public class HiddenChatState : ChatState
    {
        public override void Enter()
        {
            context.UIMediator.SetupForHiddenState();
        }

        public override void Exit()
        {
            
        }
    }
}