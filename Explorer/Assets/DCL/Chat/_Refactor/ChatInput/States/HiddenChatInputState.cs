namespace DCL.Chat.ChatInput
{
    public class HiddenChatInputState : ChatInputState
    {
        public override void Enter()
        {
            context.ChatInputView.Hide();
        }
    }
}
