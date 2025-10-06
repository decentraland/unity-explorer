using System;
using System.Collections.Generic;

namespace DCL.Translation.Service
{
    /// <summary>
    ///     Size-bounded, O(1) LRU cache.
    ///     - Most-recently-used entries are kept at the head of a linked list.
    ///     - Least-recently-used entry sits at the tail and gets evicted on overflow.
    ///     - Get() bumps recency; Peek() does not.
    ///     - RemoveOldest() and RemoveWhere() allow controlled/bulk trims.
    /// </summary>
    public sealed class LRUCache<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> map;
        private readonly LinkedList<(TKey Key, TValue Value)> lru;

        private readonly Action<TKey, TValue>? onEvicted;

        /// <summary>
        ///     Create an LRU with the given capacity.
        ///     Optionally provide onEvicted to observe evictions (stats, index maintenance, disposal).
        /// </summary>
        public LRUCache(int capacity, Action<TKey, TValue>? onEvicted = null)
        {
            Capacity = capacity > 0 ? capacity : 200;
            this.onEvicted = onEvicted;
            map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(Capacity);
            lru = new LinkedList<(TKey, TValue)>();
        }

        /// <summary>
        ///     Number of items currently stored.
        /// </summary>
        public int Count => map.Count;

        /// <summary>
        ///     Maximum number of items before evicting.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        ///     O(1) existence check; does not bump recency.
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            return map.ContainsKey(key);
        }

        /// <summary>
        ///     Try to get a value and bump it to MRU (head).
        ///     Returns true if present, false otherwise.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (map.TryGetValue(key, out var node))
            {
                lru.Remove(node);
                lru.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        ///     Try to read a value WITHOUT changing recency.
        ///     Useful for diagnostics or when you don't want to reprioritize.
        /// </summary>
        public bool TryPeek(TKey key, out TValue value)
        {
            if (map.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        ///     Insert or update an entry and mark it MRU.
        ///     If capacity is reached, evict exactly one LRU entry and invoke onEvicted (if provided).
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            if (map.TryGetValue(key, out var node))
            {
                node.Value = (key, value); // in-place update
                lru.Remove(node);
                lru.AddFirst(node);
                return;
            }

            if (map.Count >= Capacity && lru.Last is { } last)
            {
                // eviction
                map.Remove(last.Value.Key);
                lru.RemoveLast();
                onEvicted?.Invoke(last.Value.Key, last.Value.Value);
            }

            var newNode = new LinkedListNode<(TKey, TValue)>((key, value));
            lru.AddFirst(newNode);
            map[key] = newNode;
        }

        /// <summary>
        ///     Remove a specific entry if present. Returns true and the removed value; false otherwise.
        /// </summary>
        public bool TryRemove(TKey key, out TValue value)
        {
            if (!map.TryGetValue(key, out var node))
            {
                value = default!;
                return false;
            }

            value = node.Value.Value;
            map.Remove(key);
            lru.Remove(node);

            return true;
        }

        /// <summary>
        ///     Remove up to 'count' LRU entries (from the tail). Calls onEvicted for each.
        ///     Returns number actually removed.
        /// </summary>
        public int RemoveOldest(int count)
        {
            int removed = 0;
            while (removed < count && lru.Last is { } last)
            {
                map.Remove(last.Value.Key);
                lru.RemoveLast();
                onEvicted?.Invoke(last.Value.Key, last.Value.Value);
                removed++;
            }

            return removed;
        }

        /// <summary>
        ///     Bulk-remove entries that match 'predicate'. Walks from LRU side to MRU side.
        ///     Returns number removed. Useful for channel purge or selective trims.
        /// </summary>
        public int RemoveWhere(Func<TKey, TValue, bool> predicate)
        {
            int removed = 0;
            var node = lru.Last;
            while (node != null)
            {
                var prev = node.Previous;
                var key = node.Value.Key;
                var val = node.Value.Value;

                if (predicate(key, val))
                {
                    map.Remove(key);
                    lru.Remove(node);
                    onEvicted?.Invoke(key, val);
                    removed++;
                }

                node = prev;
            }

            return removed;
        }

        /// <summary>
        ///     Remove everything without firing onEvicted.
        /// </summary>
        public void Clear()
        {
            map.Clear();
            lru.Clear();
        }
    }
}