using DCL.Chat.History;
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
            this.origin.MessageAdded += OriginOnOnMessageAdded;
        }

        private void OriginOnOnMessageAdded(ChatChannel.ChannelId channelId, ChatMessage obj)
        {
            if (Valid(obj.Message))
                MessageAdded?.Invoke(channelId, obj);
        }

        public event Action<ChatChannel.ChannelId, ChatMessage>? MessageAdded;

        public void Send(ChatChannel channel, string message, string origin)
        {
            if (Valid(message))
                this.origin.Send(channel, message, origin);
            else
                MessageAdded?.Invoke(channel.Id,
                    ChatMessage.NewFromSystem("Message with the special character is forbidden")
                );
        }

        public void Dispose()
        {
            origin.MessageAdded -= OriginOnOnMessageAdded;
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
