using DCL.Diagnostics;
using DCL.FeatureFlags;
using System;
using System.Collections.Generic;

namespace DCL.Chat.MessageBus
{
    public class ChatMessageRateLimiter
    {
        private const int DEFAULT_MESSAGES_PER_SECOND = 1;
        private const string FEATURE_FLAG_VARIANT = "config";
        private const int MAX_DICTIONARY_SIZE = 1000;

        private readonly Dictionary<string, UserRateLimitData> userRateLimitData = new ();
        private int messagesPerSecond = DEFAULT_MESSAGES_PER_SECOND;
        private long cachedCurrentSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
        private DateTime lastTimeCheck = DateTime.UtcNow;

        public bool TryAllow(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return true;

            if (userRateLimitData.Count > MAX_DICTIONARY_SIZE)
                CleanupOldEntries();

            long currentSecond = GetCurrentSecond();

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

            return true;
        }

        public void LoadConfigurationFromFeatureFlag()
        {
            if (FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.CHAT_MESSAGE_RATE_LIMIT, FEATURE_FLAG_VARIANT, out ChatMessageRateLimitConfig? config) && config.HasValue)
            {
                ChatMessageRateLimitConfig value = config.Value;
                messagesPerSecond = value.messages_per_second > 0 ? value.messages_per_second : DEFAULT_MESSAGES_PER_SECOND;
            }
        }

        private long GetCurrentSecond()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastTimeCheck).TotalSeconds >= 1.0)
            {
                cachedCurrentSecond = now.Ticks / TimeSpan.TicksPerSecond;
                lastTimeCheck = now;
            }
            return cachedCurrentSecond;
        }

        private void CleanupOldEntries()
        {
            long currentSecond = GetCurrentSecond();
            var keysToRemove = new List<string>();

            foreach (var kvp in userRateLimitData)
            {
                if (currentSecond - kvp.Value.CurrentSecond > 1)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (string key in keysToRemove)
                userRateLimitData.Remove(key);
        }

        [Serializable]
        private struct ChatMessageRateLimitConfig
        {
            public int messages_per_second;
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
