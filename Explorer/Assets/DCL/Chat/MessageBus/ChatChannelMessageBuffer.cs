using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
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

        private volatile bool isLoopRunning;
        private CancellationToken? externalCancellationToken;
        private readonly object loopLock = new object();
        private long cachedCurrentSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
        private DateTime lastTimeCheck = DateTime.UtcNow;

        public event Action<ChatMessage>? MessageReleased;

        public bool HasCapacity() =>
            messageQueue.Count < maxBufferSize;

        public bool TryEnqueue(ChatMessage message)
        {
            if (!HasCapacity())
                return false;

            messageQueue.Enqueue(message);

            if (!isLoopRunning)
            {
                lock (loopLock)
                {
                    if (!isLoopRunning && externalCancellationToken?.IsCancellationRequested != true)
                        RestartLoop();
                }
            }

            return true;
        }

        public void Start(CancellationToken cancellationToken)
        {
            LoadConfigurationFromFeatureFlag();
            externalCancellationToken = cancellationToken;
            RestartLoop();
        }

        private void RestartLoop()
        {
            if (externalCancellationToken == null)
                return;

            isLoopRunning = true;
            ReleaseBufferedMessagesAsync(externalCancellationToken.Value).Forget();
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
                try
                {
                    if (messageQueue.Count == 0)
                    {
                        isLoopRunning = false;
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);

                    messagesReleasedThisFrame = 0;
                    framesSinceLastRelease++;

                    while (CanReleaseMessage())
                    {
                        try
                        {
                            messagesReleasedThisSecond++;
                            messagesReleasedThisFrame++;
                            framesSinceLastRelease = 0;
                            var message = messageQueue.Dequeue();
                            MessageReleased?.Invoke(message);
                        }
                        catch (Exception e)
                        {
                            ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    isLoopRunning = false;
                    break;
                }
                catch (Exception e)
                {
                    ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES);
                    isLoopRunning = false;
                    break;
                }
            }

            isLoopRunning = false;
        }

        private bool CanReleaseMessage()
        {
            if (framesSinceLastRelease < framesBetweenReleases)
                return false;

            if (messageQueue.Count == 0)
                return false;

            if (messagesReleasedThisFrame >= maxMessagesPerFrame)
                return false;

            long nowSecond = GetCurrentSecond();

            if (currentSecond != nowSecond)
            {
                currentSecond = nowSecond;
                messagesReleasedThisSecond = 0;
            }
            else
            {
                if (messagesReleasedThisSecond >= maxMessagesPerSecond)
                    return false;
            }


            return true;
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
