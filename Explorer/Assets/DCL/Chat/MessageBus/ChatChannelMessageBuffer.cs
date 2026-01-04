using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class ChatChannelMessageBuffer
    {
        private const int DEFAULT_MILLISECONDS_BETWEEN_RELEASES = 50;
        private const int DEFAULT_MAX_MESSAGES_PER_BURST = 1;
        private const int DEFAULT_MAX_BUFFER_SIZE = 1000;
        private readonly object loopLock = new ();

        private int millisecondsBetweenReleases = DEFAULT_MILLISECONDS_BETWEEN_RELEASES;
        private int maxMessagesPerBurst = DEFAULT_MAX_MESSAGES_PER_BURST;
        private int maxBufferSize = DEFAULT_MAX_BUFFER_SIZE;

        private Queue<ChatMessage> messageQueue = new (DEFAULT_MAX_BUFFER_SIZE);

        private DateTime lastReleaseTime = DateTime.MinValue;

        private volatile bool isLoopRunning;
        private CancellationToken? externalCancellationToken;

        public event Action<ChatMessage>? MessageReleased;

        public bool HasCapacity() =>
            messageQueue.Count < maxBufferSize;

        public bool TryEnqueue(ChatMessage message)
        {
            if (!HasCapacity()) return false;

            messageQueue.Enqueue(message);

            if (isLoopRunning) return true;

            lock (loopLock)
            {
                if (!isLoopRunning && externalCancellationToken?.IsCancellationRequested != true)
                    RestartLoop();
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
            if (FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.CHAT_MESSAGE_BUFFER_CONFIG, FeatureFlagsStrings.CONFIG_VARIANT, out ChatMessageBufferConfig? config) && config.HasValue)
            {
                ChatMessageBufferConfig value = config.Value;
                millisecondsBetweenReleases = value.MillisecondsBetweenReleases > 0 ? value.MillisecondsBetweenReleases : DEFAULT_MILLISECONDS_BETWEEN_RELEASES;
                maxMessagesPerBurst = value.MaxMessagesPerBurst > 0 ? value.MaxMessagesPerBurst : DEFAULT_MAX_MESSAGES_PER_BURST;

                int newBufferSize = value.MaxBufferSize > 0 ? value.MaxBufferSize : DEFAULT_MAX_BUFFER_SIZE;

                if (newBufferSize != maxBufferSize)
                    messageQueue = new Queue<ChatMessage>(newBufferSize);

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
            lastReleaseTime = DateTime.MinValue;
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

                    var messagesReleasedThisBurst = 0;

                    while (CanReleaseMessage(messagesReleasedThisBurst))
                    {
                        try
                        {
                            ChatMessage message = messageQueue.Dequeue();
                            lastReleaseTime = DateTime.UtcNow;
                            messagesReleasedThisBurst++;
                            MessageReleased?.Invoke(message);
                        }
                        catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }

                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }

                    await UniTask.Delay(millisecondsBetweenReleases, cancellationToken: ct);
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

        private bool CanReleaseMessage(int messagesReleasedThisBurst)
        {
            if (messageQueue.Count == 0)
                return false;

            if (messagesReleasedThisBurst >= maxMessagesPerBurst)
                return false;

            if (messagesReleasedThisBurst > 0)
                return true;

            if (lastReleaseTime == DateTime.MinValue)
                return true;

            return (DateTime.UtcNow - lastReleaseTime).TotalMilliseconds >= millisecondsBetweenReleases;
        }

        [Serializable]
        private struct ChatMessageBufferConfig
        {
            [JsonProperty("milliseconds_between_releases")] public int MillisecondsBetweenReleases;
            [JsonProperty("max_messages_per_burst")] public int MaxMessagesPerBurst;
            [JsonProperty("max_buffer_size")] public int MaxBufferSize;
        }
    }
}
