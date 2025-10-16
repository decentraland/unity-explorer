using System;
using DCL.Diagnostics;

namespace DCL.Translation.Service
{
    /// <summary>
    ///     Holds UI-facing translation state per messageId (Original/Pending/Success/Failed).
    ///     Drives spinners, error badges, and the displayed text in the feed.
    /// </summary>
    public sealed class InMemoryTranslationMemory : ITranslationMemory, IDisposable
    {
        private readonly LRUCache<string, MessageTranslation> memory;
        private readonly Action<string, MessageTranslation>? onEvictedExternal;

        /// <summary>Total number of entries evicted by the underlying LRU.</summary>
        public int Evictions { get; private set; }

        /// <summary>
        ///     Create the memory store.
        /// </summary>
        /// <param name="capacity">Max number of message states kept.</param>
        /// <param name="onEvicted">Optional external hook invoked on eviction (stats/logging).</param>
        public InMemoryTranslationMemory(int capacity = 200,
            Action<string, MessageTranslation>? onEvicted = null)
        {
            onEvictedExternal = onEvicted;
            memory = new LRUCache<string, MessageTranslation>(capacity, OnEvicted);
        }

        /// <summary>Current entry count.</summary>
        public int Count => memory.Count;

        /// <summary>Configured capacity.</summary>
        public int Capacity => memory.Capacity;

        /// <summary>
        ///     Try to read the UI state for a messageId. Bumps recency on hit.
        /// </summary>
        public bool TryGet(string messageId, out MessageTranslation translation)
        {
            return memory.TryGetValue(messageId, out translation);
        }

        /// <summary>
        ///     Set or update the UI state for a messageId. Bumps recency.
        /// </summary>
        public void Set(string messageId, MessageTranslation translation)
        {
            int before = memory.Count;

            if (Count >= Capacity)
                RemoveOldestSafe(1);

            memory.Set(messageId, translation);

            ReportHub.Log(ReportData.UNSPECIFIED,
                $"TranslationMemory.Set {messageId} count {before}->{memory.Count}/{memory.Capacity}");
        }

        /// <summary>
        ///     Remove a specific message state if present.
        /// </summary>
        public bool Remove(string messageId)
        {
            return memory.TryRemove(messageId, out _);
        }

        /// <summary>
        ///     Trim up to 'count' oldest entries BUT keep Pending entries (to avoid UI stalls).
        ///     Useful for low-memory handlers or proactive trims.
        /// </summary>
        public int RemoveOldestSafe(int count)
        {
            int removed = 0;

            removed = memory.RemoveWhere((_, mt) =>
            {
                if (removed >= count) return false;
                if (mt.State == TranslationState.Pending) return false;
                removed++;
                return true;
            });

            return removed;
        }

        /// <summary>
        ///     Convenience: set the translated result into the existing UI state if present.
        /// </summary>
        public void SetTranslatedResult(string messageId, TranslationResult result)
        {
            if (memory.TryGetValue(messageId, out var translation))
                translation.SetTranslatedResult(result.TranslatedText, result.DetectedSourceLanguage);
        }

        /// <summary>
        ///     Drop everything (used on identity change, full reset, or scene unload).
        /// </summary>
        public void Clear()
        {
            memory.Clear();
        }

        /// <summary>
        ///     Internal eviction hook increments local counter and forwards to external telemetry if provided.
        /// </summary>
        private void OnEvicted(string messageId, MessageTranslation mt)
        {
            Evictions++;
            onEvictedExternal?.Invoke(messageId, mt);
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
