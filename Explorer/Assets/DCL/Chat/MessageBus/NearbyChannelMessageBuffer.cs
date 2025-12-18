using DCL.Chat.History;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace DCL.Chat.MessageBus
{
    public class NearbyChannelMessageBuffer
    {
        private const int MAX_MESSAGES_PER_SECOND = 30;
        public const int MAX_MESSAGES_PER_FRAME = 1;
        private const int MAX_BUFFER_SIZE = 1000;
        private readonly Queue<ChatMessage> messageQueue = new (1000);
        private long currentSecond;
        private int messageCount;

        public bool TryEnqueue(ChatMessage message)
        {
            if (messageQueue.Count >= MAX_BUFFER_SIZE)
                return false;

            messageQueue.Enqueue(message);
            return true;
        }

        private bool HasCapacity()
        {
            long nowSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            if (currentSecond != nowSecond)
            {
                currentSecond = nowSecond;
                messageCount = 0;
            }

            return messageCount < MAX_MESSAGES_PER_SECOND;
        }

        public bool TryDequeue(out ChatMessage message)
        {
            if (messageQueue.Count == 0 || !HasCapacity())
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"Cant Dequeue - queue has {messageQueue.Count} - sent messages this second {messageCount} ");
                message = default(ChatMessage);
                return false;
            }

            messageCount++;
            message = messageQueue.Dequeue();
            return true;
        }

        public bool HasBufferedMessages => messageQueue.Count > 0;

        public int BufferedCount => messageQueue.Count;
    }
}

