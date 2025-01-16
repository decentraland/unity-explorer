using System;
using System.Collections.Generic;

namespace DCL.Chat.History
{
    public interface IChatHistory
    {
        event Action Cleared;

        event Action<ChatMessage> MessageAdded;

        IReadOnlyList<ChatMessage> Messages { get; }

        void AddMessage(ChatMessage message);

        void ForceUpdateMessage(int inIndex, ChatMessage message);

        void Clear();
    }
}
