using MVC;

namespace DCL.Chat.ChatInput
{
    public class HiddenChatInputState : ChatInputState, IState
    {
        private readonly ChatInputView view;

        public HiddenChatInputState(ChatInputView view)
        {
            this.view = view;
        }

        public void Enter()
        {
            view.Hide();
        }
    }
}
