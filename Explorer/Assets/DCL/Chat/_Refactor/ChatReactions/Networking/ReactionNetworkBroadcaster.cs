using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Core;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    /// Buffers outgoing situational reactions through a debounce window and flushes
    /// batched (emoji -> count) payloads to the network bus.
    /// When debounce returns 0, the underlying debouncer sends immediately without buffering.
    /// </summary>
    internal sealed class ReactionNetworkBroadcaster : IDisposable
    {
        private readonly IReactionMessageBus? reactionBus;
        private readonly SituationalReactionDebouncer? debouncer;
        private readonly Action<int, int, float>? onFlushed;

        public ReactionNetworkBroadcaster(
            IReactionMessageBus? reactionBus,
            Func<float> getDebounceSeconds,
            Action<int, int, float>? onFlushed = null)
        {
            this.reactionBus = reactionBus;
            this.onFlushed = onFlushed;

            if (reactionBus != null)
                debouncer = new SituationalReactionDebouncer(Flush, getDebounceSeconds);
        }

        public void Broadcast(int emojiIndex)
        {
            debouncer?.Add(emojiIndex);
        }

        public void Tick(float dt)
        {
            debouncer?.Tick(dt);
        }

        public void Dispose()
        {
            debouncer?.Dispose();
        }

        private void Flush(Dictionary<int, int> emojiCounts)
        {
            float baseTimestamp = Time.unscaledTime;
            int offset = 0;
            int totalCount = 0;

            foreach (var kvp in emojiCounts)
            {
                reactionBus?.SendSituationalReaction(kvp.Key, kvp.Value, baseTimestamp + offset * 0.001f);
                totalCount += kvp.Value;
                offset++;
            }

            onFlushed?.Invoke(emojiCounts.Count, totalCount, baseTimestamp);
        }
    }
}
