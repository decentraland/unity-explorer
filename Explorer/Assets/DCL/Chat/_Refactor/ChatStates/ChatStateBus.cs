using System;

namespace DCL.Chat.ChatStates
{
    public class ChatStateBus
    {
        public event Action<ChatMainController>? StateChanged;

        internal void InvokeStateChanged(ChatMainController controller) =>
            StateChanged?.Invoke(controller);
    }
}
