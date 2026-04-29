using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    /// Buffers outgoing situational reactions through a configurable debounce window,
    /// deduplicating same-emoji clicks, and flushes batched payloads to the network bus.
    /// </summary>
    internal sealed class ReactionNetworkBroadcaster : IDisposable
    {
        private readonly IReactionMessageBus? reactionBus;
        private readonly ChatReactionsConfig config;
        private readonly Dictionary<int, int> bufferedEmojis = new ();
        private float timer;
        private int bufferedCount;
        private bool active;

        /// <summary>
        /// Fired when a debounced batch is flushed to the network.
        /// Parameters: uniqueEmojiCount, totalCount, unscaledTimestamp.
        /// </summary>
        public event Action<int, int, float>? Flushed;

        public ReactionNetworkBroadcaster(
            ChatReactionsConfig config,
            IReactionMessageBus? reactionBus)
        {
            this.config = config;
            this.reactionBus = reactionBus;
        }

        public void Broadcast(int emojiIndex)
        {
            if (reactionBus == null) return;

            if (bufferedEmojis.TryGetValue(emojiIndex, out int existing))
                bufferedEmojis[emojiIndex] = existing + 1;
            else
                bufferedEmojis[emojiIndex] = 1;

            bufferedCount++;

            float debounce = config.MessageReactions.NetworkDebounceSeconds;
            int threshold = config.MessageReactions.NetworkFlushThreshold;

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

            Profiler.BeginSample("ChatReactions.Network.Flush");

            float baseTimestamp = Time.unscaledTime;
            int offset = 0;
            int totalCount = 0;

            foreach (var kvp in bufferedEmojis)
            {
                reactionBus!.SendSituationalReaction(kvp.Key, kvp.Value, baseTimestamp + offset * 0.001f);
                totalCount += kvp.Value;
                offset++;
            }

            int uniqueCount = bufferedEmojis.Count;
            bufferedEmojis.Clear();

            Flushed?.Invoke(uniqueCount, totalCount, baseTimestamp);

            Profiler.EndSample();
        }
    }
}
