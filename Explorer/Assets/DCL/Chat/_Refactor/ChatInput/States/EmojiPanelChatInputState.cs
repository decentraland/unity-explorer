using MVC;

namespace DCL.Chat
{
    public class EmojiPanelChatInputState : IndependentMVCState<ChatInputStateContext>
    {
        public EmojiPanelChatInputState(ChatInputStateContext context) : base(context) { }

        protected override void Activate(ControllerNoData input) { }

        protected override void Deactivate() { }
    }
}
