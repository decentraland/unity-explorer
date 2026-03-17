using System.Collections.Generic;

namespace DCL.Chat.History
{
    public class ReactionSet
    {
        private readonly Dictionary<int, HashSet<string>> reactions = new ();
        private readonly List<int> insertionOrder = new ();

        public bool IsEmpty => insertionOrder.Count == 0;

        public bool AddReaction(int emojiIndex, string walletAddress)
        {
            if (!reactions.TryGetValue(emojiIndex, out HashSet<string>? wallets))
            {
                wallets = new HashSet<string>();
                reactions[emojiIndex] = wallets;
                insertionOrder.Add(emojiIndex);
            }

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

        public IReadOnlyCollection<string>? GetReactors(int emojiIndex)
        {
            return reactions.TryGetValue(emojiIndex, out HashSet<string>? wallets)
                ? wallets
                : null;
        }

        public void Clear()
        {
            reactions.Clear();
            insertionOrder.Clear();
        }
    }
}
