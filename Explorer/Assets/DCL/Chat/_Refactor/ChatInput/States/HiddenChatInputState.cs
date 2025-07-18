namespace DCL.Chat
{
    public class HiddenChatInputState : ChatInputState
    {
        public override void Begin()
        {
            context.ChatInputView.Hide();
        }
    }
}
