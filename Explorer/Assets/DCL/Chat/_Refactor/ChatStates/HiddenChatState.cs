#if !NO_LIVEKIT_MODE

namespace DCL.Chat.ChatStates
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

#endif
