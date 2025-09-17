using System;

namespace DCL.Chat.ChatStates
{
    public class ChatStateBus
    {
        public event Action<ChatState>? StateChanged;

        internal void InvokeStateChanged(ChatState currentState) =>
            StateChanged?.Invoke(currentState);
    }
}
