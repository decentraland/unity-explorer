using MVC;

namespace DCL.Chat
{
    public abstract class ChatInputState : MVCState<ChatInputState, ChatInputStateContext>
    {
        internal void OnBlockedUpdated(bool isUnblocked)
        {
            if (isUnblocked)
                OnInputUnblocked();
            else
                OnInputBlocked();
        }

        protected virtual void OnInputBlocked() { }

        protected virtual void OnInputUnblocked() { }
    }
}
