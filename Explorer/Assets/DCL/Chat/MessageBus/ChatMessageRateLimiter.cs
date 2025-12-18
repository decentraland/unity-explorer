using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace DCL.Chat.MessageBus
{
    public class ChatMessageRateLimiter
    {
        private readonly Dictionary<string, UserRateLimitData> userRateLimitData = new ();

        public bool TryAllow(string sourceId, int messagesPerSecond)
        {
            if (string.IsNullOrEmpty(sourceId))
                return true;

            long currentSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            if (!userRateLimitData.TryGetValue(sourceId, out UserRateLimitData data))
            {
                data = new UserRateLimitData(currentSecond);
                userRateLimitData[sourceId] = data;
            }

            if (data.CurrentSecond != currentSecond)
            {
                data.CurrentSecond = currentSecond;
                data.Count = 0;
            }

            if (data.Count >= messagesPerSecond)
            {
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"Rate limit exceeded for sender {sourceId}");
                return false;
            }

            data.Count++;
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"Rate limit allowed for sender {sourceId} with current second {currentSecond} and count {data.Count}");

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
