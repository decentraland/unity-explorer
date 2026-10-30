using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    /// Allocation-free per-sender token-bucket rate limiter for network intake.
    /// Unlike <see cref="Core.TokenBucketRateLimiter"/> it needs no external tick:
    /// tokens are refilled lazily from the timestamp passed to <see cref="TryPass"/>.
    /// Memory is bounded: when the tracked-sender table is full, senders idle long
    /// enough to be fully refilled are evicted first; if none qualify the message is
    /// dropped (fail closed), so identity-spoofing floods cannot grow the table.
    /// </summary>
    internal sealed class PerSenderRateLimiter
    {
        private readonly float tokensPerSecond;
        private readonly float burstCapacity;
        private readonly int maxTrackedSenders;
        private readonly Dictionary<string, Entry> entries;
        private readonly List<string> evictionScratch = new ();

        public PerSenderRateLimiter(float tokensPerSecond, float burstCapacity, int maxTrackedSenders)
        {
            this.tokensPerSecond = tokensPerSecond;
            this.burstCapacity = burstCapacity;
            this.maxTrackedSenders = maxTrackedSenders;
            entries = new Dictionary<string, Entry>(maxTrackedSenders);
        }

        /// <summary>
        /// Consumes one token for the sender. Returns false when the sender exceeded
        /// its budget or the table is full of active senders (message must be dropped).
        /// </summary>
        public bool TryPass(string senderId, float now)
        {
            if (entries.TryGetValue(senderId, out Entry entry))
            {
                float tokens = Mathf.Min(burstCapacity, entry.Tokens + ((now - entry.LastSeenTime) * tokensPerSecond));
                bool pass = tokens >= 1f;

                if (pass)
                    tokens -= 1f;

                entries[senderId] = new Entry(tokens, now);
                return pass;
            }

            if (entries.Count >= maxTrackedSenders && !TryEvictRefilledEntries(now))
                return false;

            entries[senderId] = new Entry(burstCapacity - 1f, now);
            return true;
        }

        private bool TryEvictRefilledEntries(float now)
        {
            // An entry idle for burstCapacity / tokensPerSecond seconds is fully refilled,
            // so forgetting it is indistinguishable from keeping it.
            float refillHorizon = burstCapacity / tokensPerSecond;

            evictionScratch.Clear();

            foreach (KeyValuePair<string, Entry> kvp in entries)
            {
                if (now - kvp.Value.LastSeenTime >= refillHorizon)
                    evictionScratch.Add(kvp.Key);
            }

            for (int i = 0; i < evictionScratch.Count; i++)
                entries.Remove(evictionScratch[i]);

            return evictionScratch.Count > 0;
        }

        private readonly struct Entry
        {
            public readonly float Tokens;
            public readonly float LastSeenTime;

            public Entry(float tokens, float lastSeenTime)
            {
                Tokens = tokens;
                LastSeenTime = lastSeenTime;
            }
        }
    }
}
