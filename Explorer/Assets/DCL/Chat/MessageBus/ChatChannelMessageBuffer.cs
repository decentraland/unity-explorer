using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.FeatureFlags;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class ChatChannelMessageBuffer
    {
        private const int DEFAULT_MAX_MESSAGES_PER_SECOND = 30;
        private const int DEFAULT_MAX_MESSAGES_PER_FRAME = 1;
        private const int DEFAULT_FRAMES_BETWEEN_RELEASES = 3;
        private const int DEFAULT_MAX_BUFFER_SIZE = 1000;
        private const string FEATURE_FLAG_VARIANT = "config";

        private int maxMessagesPerSecond = DEFAULT_MAX_MESSAGES_PER_SECOND;
        private int maxMessagesPerFrame = DEFAULT_MAX_MESSAGES_PER_FRAME;
        private int framesBetweenReleases = DEFAULT_FRAMES_BETWEEN_RELEASES;
        private int maxBufferSize = DEFAULT_MAX_BUFFER_SIZE;
        private Queue<ChatMessage> messageQueue = new Queue<ChatMessage>(DEFAULT_MAX_BUFFER_SIZE);

        private long currentSecond;
        private int messagesReleasedThisSecond;
        private int messagesReleasedThisFrame;
        private int framesSinceLastRelease;

        public event Action<ChatMessage>? MessageReleased;

        public bool HasCapacity() =>
            messageQueue.Count < maxBufferSize;

        public bool TryEnqueue(ChatMessage message)
        {
            if (!HasCapacity())
                return false;

            messageQueue.Enqueue(message);
            return true;
        }

        public void Start(CancellationToken cancellationToken)
        {
            LoadConfigurationFromFeatureFlag();
            ReleaseBufferedMessagesAsync(cancellationToken).Forget();
        }

        private void LoadConfigurationFromFeatureFlag()
        {
            if (FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.CHAT_MESSAGE_BUFFER_CONFIG, FEATURE_FLAG_VARIANT, out ChatMessageBufferConfig? config) && config.HasValue)
            {
                ChatMessageBufferConfig value = config.Value;
                maxMessagesPerSecond = value.max_messages_per_second > 0 ? value.max_messages_per_second : DEFAULT_MAX_MESSAGES_PER_SECOND;
                maxMessagesPerFrame = value.max_messages_per_frame > 0 ? value.max_messages_per_frame : DEFAULT_MAX_MESSAGES_PER_FRAME;
                framesBetweenReleases = value.frames_between_releases > 0 ? value.frames_between_releases : DEFAULT_FRAMES_BETWEEN_RELEASES;
                
                int newBufferSize = value.max_buffer_size > 0 ? value.max_buffer_size : DEFAULT_MAX_BUFFER_SIZE;
                if (newBufferSize != maxBufferSize)
                {
                    messageQueue = new Queue<ChatMessage>(newBufferSize);
                }
                maxBufferSize = newBufferSize;
            }
        }

        public void Dispose()
        {
            messageQueue.Clear();
        }

        public void Reset()
        {
            messageQueue.Clear();
            messagesReleasedThisSecond = 0;
            messagesReleasedThisFrame = 0;
            framesSinceLastRelease = 0;
            currentSecond = 0;
        }

        private async UniTaskVoid ReleaseBufferedMessagesAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                framesSinceLastRelease++;

                if (TryRelease(out ChatMessage message))
                    MessageReleased?.Invoke(message);

                messagesReleasedThisFrame = 0;
            }
        }

        private bool TryRelease(out ChatMessage message)
        {
            message = default(ChatMessage);

            if (framesSinceLastRelease < framesBetweenReleases)
                return false;

            if (messageQueue.Count == 0)
                return false;

            long nowSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            if (currentSecond != nowSecond)
            {
                currentSecond = nowSecond;
                messagesReleasedThisSecond = 0;
            }

            if (messagesReleasedThisSecond >= maxMessagesPerSecond)
                return false;

            if (messagesReleasedThisFrame >= maxMessagesPerFrame)
                return false;

            messagesReleasedThisSecond++;
            messagesReleasedThisFrame++;
            framesSinceLastRelease = 0;
            message = messageQueue.Dequeue();
            return true;
        }

        [Serializable]
        private struct ChatMessageBufferConfig
        {
            public int max_messages_per_second;
            public int max_messages_per_frame;
            public int frames_between_releases;
            public int max_buffer_size;
        }
    }
}
