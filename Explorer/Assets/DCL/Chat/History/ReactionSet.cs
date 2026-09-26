using System.Collections.Generic;

namespace DCL.Chat.History
{
    public class ReactionSet
    {
        /// <summary>
        /// Hard ceiling on distinct emoji entries (keys) per message, independent of the product
        /// limit configured in ChatReactionsMessageConfig.MaxDistinctReactionsPerMessage.
        /// Bounds the number of distinct emojis tracked (remote peers, history load, local toggles).
        /// Does not bound reactors within a single emoji — see <see cref="MAX_REACTORS_PER_EMOJI"/>.
        /// </summary>
        public const int MAX_DISTINCT_EMOJIS = 20;

        /// <summary>
        /// Hard ceiling on distinct wallet addresses tracked per emoji. Bounds worst-case memory
        /// growth under a spoofed-address flood (SEC-029 relay path, where the attacker controls
        /// Payload.Address) while comfortably exceeding a realistic comms-island population
        /// (low hundreds of peers).
        /// </summary>
        public const int MAX_REACTORS_PER_EMOJI = 512;

        private readonly Dictionary<int, HashSet<string>> reactions = new ();
        private readonly List<int> insertionOrder = new ();

        public bool IsEmpty => insertionOrder.Count == 0;

        public int DistinctEmojiCount => insertionOrder.Count;

        public bool AddReaction(int emojiIndex, string walletAddress)
        {
            if (!reactions.TryGetValue(emojiIndex, out HashSet<string>? wallets))
            {
                if (insertionOrder.Count >= MAX_DISTINCT_EMOJIS)
                    return false;

                wallets = new HashSet<string>();
                reactions[emojiIndex] = wallets;
                insertionOrder.Add(emojiIndex);
            }

            if (wallets.Count >= MAX_REACTORS_PER_EMOJI && !wallets.Contains(walletAddress))
                return false;

            return wallets.Add(walletAddress);
        }

        public bool RemoveReaction(int emojiIndex, string walletAddress)
        {
            if (!reactions.TryGetValue(emojiIndex, out HashSet<string>? wallets))
                return false;

            bool removed = wallets.Remove(walletAddress);

            if (removed && wallets.Count == 0)
            {
                reactions.Remove(emojiIndex);
                insertionOrder.Remove(emojiIndex);
            }

            return removed;
        }

        public bool HasReacted(int emojiIndex, string walletAddress)
        {
            return reactions.TryGetValue(emojiIndex, out HashSet<string>? wallets)
                   && wallets.Contains(walletAddress);
        }

        /// <summary>
        /// Returns aggregate counts in insertion order (first emoji reacted on appears first).
        /// Reuses the provided list to avoid allocations. Caller must not cache the list.
        /// </summary>
        public void GetAggregateCounts(List<(int EmojiIndex, int Count)> result)
        {
            result.Clear();

            for (int i = 0; i < insertionOrder.Count; i++)
            {
                int emojiIndex = insertionOrder[i];
                result.Add((emojiIndex, reactions[emojiIndex].Count));
            }
        }

        public IReadOnlyCollection<string>? GetReactors(int emojiIndex) =>
            reactions.GetValueOrDefault(emojiIndex);

        public void Clear()
        {
            reactions.Clear();
            insertionOrder.Clear();
        }
    }
}
