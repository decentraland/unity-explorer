namespace DCL.Chat.ChatInput
{
    public class HiddenChatInputState : ChatInputState
    {
        private readonly ChatInputView view;

        public HiddenChatInputState(ChatInputView view)
        {
            this.view = view;
        }

        public override void Enter()
        {
            view.Hide();
        }
    }
}
