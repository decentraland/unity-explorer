#if !NO_LIVEKIT_MODE

namespace DCL.Chat.ChatInput
{
    public class HiddenChatInputState : ChatInputState
    {
        public override void Begin()
        {
            context.ChatInputView.Hide();
        }
    }
}

#endif
