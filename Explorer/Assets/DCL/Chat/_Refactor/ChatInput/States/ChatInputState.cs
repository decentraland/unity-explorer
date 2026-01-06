using MVC;

namespace DCL.Chat.ChatInput
{
    public abstract class ChatInputState : IExitableState
    {
        public virtual void Exit() { }

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
