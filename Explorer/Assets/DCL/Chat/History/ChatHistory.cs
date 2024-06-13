using System;
using System.Collections.Generic;

namespace DCL.Chat.History
{
    public class ChatHistory : IChatHistory
    {
        public event Action? OnCleared;
        public event Action<ChatMessage>? OnMessageAdded;

        private readonly List<ChatMessage> messages = new ();

        public IReadOnlyList<ChatMessage> Messages => messages;

        public void AddMessage(ChatMessage message)
        {
            if (messages.Count is 0)
            {
                // Adding two elements to count as top and bottom padding
                messages.Add(new ChatMessage(true));
                messages.Add(new ChatMessage(true));
            }

            //Removing padding element and reversing list due to infinite scroll view behaviour
            messages.Remove(messages[^1]);
            messages.Reverse();
            messages.Add(message);
            messages.Add(new ChatMessage(true));
            messages.Reverse();

            OnMessageAdded?.Invoke(message);
        }

        public void ForceUpdateMessage(int inIndex, ChatMessage message)
        {
            messages[inIndex] = message;
        }

        public void Clear()
        {
            messages.Clear();
            OnCleared?.Invoke();
        }
    }
}
