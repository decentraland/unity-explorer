using MVC;

namespace DCL.Chat
{
    public abstract class ChatInputState : MVCState<ChatInputState, ChatInputStateContext>
    {
        public void OnBlockedUpdated(bool isUnblocked)
        {
            if (isUnblocked)
                OnInputUnblocked();
            else
                ChangeState<BlockedChatInputState>();
        }

        protected virtual void OnInputUnblocked() { }
    }
}
