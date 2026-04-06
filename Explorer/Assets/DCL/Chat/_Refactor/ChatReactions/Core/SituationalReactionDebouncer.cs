using System;
using System.Collections.Generic;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Buffers situational reaction emoji clicks within a configurable time window,
    /// deduplicating same-emoji spam and tracking per-emoji click counts.
    /// On timer expiry, flushes the accumulated (emoji → count) dictionary to the callback.
    /// </summary>
    public sealed class SituationalReactionDebouncer : IDisposable
    {
        private readonly Dictionary<int, int> bufferedEmojis = new ();
        private readonly Action<Dictionary<int, int>> onFlush;
        private readonly Func<float> getDebounceSeconds;
        private readonly Func<int> getFlushThreshold;
        private float timer;
        private int bufferedCount;
        private bool active;

        public SituationalReactionDebouncer(
            Action<Dictionary<int, int>> onFlush,
            Func<float> getDebounceSeconds,
            Func<int> getFlushThreshold)
        {
            this.onFlush = onFlush;
            this.getDebounceSeconds = getDebounceSeconds;
            this.getFlushThreshold = getFlushThreshold;
        }

        public void Add(int emojiIndex)
        {
            if (bufferedEmojis.TryGetValue(emojiIndex, out int existing))
                bufferedEmojis[emojiIndex] = existing + 1;
            else
                bufferedEmojis[emojiIndex] = 1;

            bufferedCount++;

            float debounce = getDebounceSeconds();
            int threshold = getFlushThreshold();

            if (debounce <= 0f || (threshold > 0 && bufferedCount >= threshold))
            {
                Flush();
                return;
            }

            timer = debounce;
            active = true;
        }

        public void Tick(float dt)
        {
            if (!active) return;

            timer -= dt;

            if (timer > 0f) return;

            Flush();
        }

        public void Dispose()
        {
            // Intentionally do NOT flush on dispose.
            // During teardown the network bus may already be disposed (scope uses FIFO order),
            // so flushing here would silently drop reactions. Buffered reactions during
            // shutdown are expendable — the user is leaving the context.
            bufferedEmojis.Clear();
            active = false;
        }

        private void Flush()
        {
            active = false;
            bufferedCount = 0;

            if (bufferedEmojis.Count == 0)
                return;

            onFlush(bufferedEmojis);
            bufferedEmojis.Clear();
        }
    }
}
