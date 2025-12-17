using System;
using System.Collections.Generic;

namespace DCL.Chat.MessageBus.RateLimiting
{
    public class ChatMessageRateLimiter
    {
        private readonly int messagesPerSecond;
        private readonly Dictionary<string, UserRateLimitData> userRateLimitData = new ();

        public ChatMessageRateLimiter(int messagesPerSecond)
        {
            this.messagesPerSecond = messagesPerSecond;
        }

        public bool TryAllow(string walletId)
        {
            if (string.IsNullOrEmpty(walletId))
                return true;

            long currentSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            if (!userRateLimitData.TryGetValue(walletId, out UserRateLimitData data))
            {
                data = new UserRateLimitData(currentSecond);
                userRateLimitData[walletId] = data;
            }

            if (data.CurrentSecond != currentSecond)
            {
                data.CurrentSecond = currentSecond;
                data.Count = 0;
            }

            if (data.Count >= messagesPerSecond)
                return false;

            data.Count++;
            return true;
        }

        private class UserRateLimitData
        {
            public long CurrentSecond;
            public int Count;

            public UserRateLimitData(long currentSecond)
            {
                CurrentSecond = currentSecond;
                Count = 0;
            }
        }
    }
}
