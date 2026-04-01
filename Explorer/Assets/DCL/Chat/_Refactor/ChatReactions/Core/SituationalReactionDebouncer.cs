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
        private float timer;
        private bool active;

        public SituationalReactionDebouncer(Action<Dictionary<int, int>> onFlush, Func<float> getDebounceSeconds)
        {
            this.onFlush = onFlush;
            this.getDebounceSeconds = getDebounceSeconds;
        }

        public void Add(int emojiIndex)
        {
            if (bufferedEmojis.TryGetValue(emojiIndex, out int existing))
                bufferedEmojis[emojiIndex] = existing + 1;
            else
                bufferedEmojis[emojiIndex] = 1;

            float debounce = getDebounceSeconds();

            if (debounce <= 0f)
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
            if (bufferedEmojis.Count > 0)
                Flush();
        }

        private void Flush()
        {
            active = false;

            if (bufferedEmojis.Count == 0)
                return;

            onFlush(bufferedEmojis);
            bufferedEmojis.Clear();
        }
    }
}
