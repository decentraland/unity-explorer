using System;
using System.Collections.Generic;

namespace DCL.Chat.MessageBus
{
    /// <summary>
    /// Fast fix, should be replaced with a more robust solution.
    /// </summary>
    public class IgnoreWithSymbolsChatMessageBus : IChatMessagesBus
    {
        private readonly IChatMessagesBus origin;
        private readonly ISet<char> forbiddenChars;

        public IgnoreWithSymbolsChatMessageBus(IChatMessagesBus origin) : this(origin, new HashSet<char>
        {
            //strange ping pong messages from the previous client
            '␐',
            '␆',
            '␑'
        }) { }

        public IgnoreWithSymbolsChatMessageBus(IChatMessagesBus origin, ISet<char> forbiddenChars)
        {
            this.origin = origin;
            this.forbiddenChars = forbiddenChars;
            this.origin.OnMessageAdded += OriginOnOnMessageAdded;
        }

        private void OriginOnOnMessageAdded(ChatMessage obj)
        {
            if (Valid(obj.Message))
                OnMessageAdded?.Invoke(obj);
        }

        public event Action<ChatMessage>? OnMessageAdded;

        public void Send(string message)
        {
            if (Valid(message))
                origin.Send(message);
            else
                OnMessageAdded?.Invoke(
                    ChatMessage.NewFromSystem("Message with the special character is forbidden")
                );
        }

        public void Dispose()
        {
            origin.OnMessageAdded -= OriginOnOnMessageAdded;
            origin.Dispose();
        }

        private bool Valid(string message)
        {
            for (var i = 0; i < message.Length; i++)
                if (forbiddenChars.Contains(message[i]))
                    return false;

            return true;
        }
    }
}
