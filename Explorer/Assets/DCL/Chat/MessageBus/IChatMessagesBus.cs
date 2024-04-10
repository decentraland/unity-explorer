using System;

namespace DCL.Chat
{
    public interface IChatMessagesBus : IDisposable
    {
        public event Action<ChatMessage> OnMessageAdded;

        public void Send(string message);
    }
}
