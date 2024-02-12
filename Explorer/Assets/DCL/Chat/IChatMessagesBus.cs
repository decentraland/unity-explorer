using System;

namespace DCL.Chat
{
    public interface IChatMessagesBus
    {
        public event Action<ChatMessage> OnMessageAdded;

        public void AddMessage();
    }
}
