using System;
using System.Collections.Generic;

namespace DCL.Chat.History
{
    public interface IChatHistory
    {
        event Action OnCleared;

        IReadOnlyList<ChatMessage> Messages { get; }

        void AddMessage(ChatMessage message);

        void ForceUpdateMessage(int inIndex, ChatMessage message);

        void Clear();
    }
}
