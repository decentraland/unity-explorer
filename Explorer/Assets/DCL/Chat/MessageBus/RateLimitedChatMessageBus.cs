using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
using System;

namespace DCL.Chat.MessageBus
{
    public class RateLimitedChatMessageBus : IChatMessagesBus
    {
        private readonly IChatMessagesBus origin;
        private readonly ChatMessageRateLimiter rateLimiter;

        public event Action<ChatChannel.ChannelId, ChatChannel.ChatChannelType, ChatMessage>? MessageAdded;

        public RateLimitedChatMessageBus(IChatMessagesBus origin, int messagesPerSecond)
        {
            this.origin = origin;
            this.rateLimiter = new ChatMessageRateLimiter(messagesPerSecond);
            this.origin.MessageAdded += OriginOnOnMessageAdded;
        }

        public void Dispose()
        {
            origin.MessageAdded -= OriginOnOnMessageAdded;
            origin.Dispose();
        }

        private void OriginOnOnMessageAdded(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, ChatMessage message)
        {
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"RateLimitedChatMessageBus received message from {message.SenderWalletId}, IsSystem: {message.IsSystemMessage}, IsOwnUser: {message.IsSentByOwnUser}");

            if (message.IsSystemMessage || message.IsSentByOwnUser)
            {
                MessageAdded?.Invoke(channelId, channelType, message);
                return;
            }

            if (rateLimiter.TryAllow(message.SenderWalletId))
                MessageAdded?.Invoke(channelId, channelType, message);

        }

        public void Send(ChatChannel channel, string message, ChatMessageOrigin origin, double timestamp)
        {
            this.origin.Send(channel, message, origin, timestamp);
        }
    }
}

