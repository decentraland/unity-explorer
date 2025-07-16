namespace DCL.Chat._Refactor.ChatStates
{
    public class HiddenChatState : ChatState
    {
        public override void Begin()
        {
            context.UIMediator.SetupForHiddenState();
        }

        public override void End()
        {
            
        }
    }
}