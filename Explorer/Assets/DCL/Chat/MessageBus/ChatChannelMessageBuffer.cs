using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat.MessageBus
{
    public class ChatChannelMessageBuffer
    {
        private const int MAX_MESSAGES_PER_SECOND = 30;
        private const int MAX_MESSAGES_PER_FRAME = 1;
        private const int FRAMES_BETWEEN_RELEASES = 3;
        private const int MAX_BUFFER_SIZE = 1000;
        private readonly Queue<ChatMessage> messageQueue = new (MAX_BUFFER_SIZE);

        private long currentSecond;
        private int messagesReleasedThisSecond;
        private int messagesReleasedThisFrame;
        private int framesSinceLastRelease;

        public event Action<ChatMessage>? MessageReleased;

        public bool HasCapacity() =>
            messageQueue.Count < MAX_BUFFER_SIZE;

        public bool TryEnqueue(ChatMessage message)
        {
            if (!HasCapacity())
                return false;

            messageQueue.Enqueue(message);
            return true;
        }

        public void Start(CancellationToken cancellationToken)
        {
            ReleaseBufferedMessagesAsync(cancellationToken).Forget();
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

            if (framesSinceLastRelease < FRAMES_BETWEEN_RELEASES)
                return false;

            if (messageQueue.Count == 0)
                return false;

            long nowSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            if (currentSecond != nowSecond)
            {
                currentSecond = nowSecond;
                messagesReleasedThisSecond = 0;
            }

            if (messagesReleasedThisSecond >= MAX_MESSAGES_PER_SECOND)
                return false;

            if (messagesReleasedThisFrame >= MAX_MESSAGES_PER_FRAME)
                return false;

            messagesReleasedThisSecond++;
            messagesReleasedThisFrame++;
            framesSinceLastRelease = 0;
            message = messageQueue.Dequeue();
            return true;
        }
    }
}
