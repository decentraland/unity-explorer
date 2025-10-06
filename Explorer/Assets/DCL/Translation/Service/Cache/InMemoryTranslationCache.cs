using System;
using System.Collections.Generic;
using DCL.Diagnostics;
using DCL.Utilities;

namespace DCL.Translation.Service
{
    /// <summary>
    ///     Stores final translation results to avoid re-paying the provider.
    ///     Keyed by (messageId, target language).
    ///     - Fast O(1) lookups with LRU eviction.
    ///     - Maintains a secondary index messageId -> { (messageId, lang) } for bulk deletion.
    /// </summary>
    public sealed class InMemoryTranslationCache : ITranslationCache
    {
        private readonly LRUCache<MessageLangKey, TranslationResult> cache;

        // Secondary index to enable "remove all languages for this message"
        private readonly Dictionary<string, HashSet<MessageLangKey>> byMessageId = new();

        // Optional external telemetry hook (e.g., to count/log evictions)
        private readonly Action<MessageLangKey, TranslationResult>? onEvictedExternal;

        /// <summary>Total number of entries evicted by the underlying LRU.</summary>
        public int Evictions { get; private set; }

        /// <summary>
        ///     Create the cache with a given capacity and optional eviction callback.
        /// </summary>
        /// <param name="capacity">Max entries before evicting the LRU item.</param>
        /// <param name="onEvicted">Optional external hook invoked on eviction.</param>
        public InMemoryTranslationCache(int capacity = 200,
            Action<MessageLangKey, TranslationResult>? onEvicted = null)
        {
            onEvictedExternal = onEvicted;
            cache = new LRUCache<MessageLangKey, TranslationResult>(capacity, OnEvicted);
        }

        /// <summary>Current entry count.</summary>
        public int Count => cache.Count;

        /// <summary>Configured capacity (max entries).</summary>
        public int Capacity => cache.Capacity;

        /// <summary>
        ///     Try to get a translated result for (messageId, targetLang).
        ///     Bumps recency on hit.
        /// </summary>
        public bool TryGet(string messageId, LanguageCode targetLang, out TranslationResult result)
        {
            return cache.TryGetValue(new MessageLangKey(messageId, targetLang), out result);
        }

        /// <summary>
        ///     Store/update a translated result for (messageId, targetLang).
        ///     Bumps recency.
        /// </summary>
        public void Set(string messageId, LanguageCode targetLang, TranslationResult result)
        {
            int before = cache.Count;
            var key = new MessageLangKey(messageId, targetLang);

            cache.Set(key, result); // LRU handles eviction if full

            // Ensure secondary index contains the key
            if (!byMessageId.TryGetValue(messageId, out var set))
            {
                set = new HashSet<MessageLangKey>();
                byMessageId[messageId] = set;
            }

            set.Add(key);

            ReportHub.Log(ReportCategory.TRANSLATE,
                $"TranslationCache.Set {messageId}:{targetLang} count {before}->{cache.Count}/{cache.Capacity}");
        }

        /// <summary>
        ///     Remove a specific (messageId, targetLang) entry if present.
        /// </summary>
        public bool Remove(string messageId, LanguageCode targetLang)
        {
            var key = new MessageLangKey(messageId, targetLang);
            bool removed = cache.TryRemove(key, out _);
            if (removed && byMessageId.TryGetValue(messageId, out var set))
            {
                set.Remove(key);
                if (set.Count == 0) byMessageId.Remove(messageId);
            }

            return removed;
        }

        /// <summary>
        ///     Remove all cached results for a given message across ALL languages.
        ///     O(k) where k = number of languages cached for that message.
        /// </summary>
        public int RemoveAllForMessage(string messageId)
        {
            if (!byMessageId.TryGetValue(messageId, out var set) || set.Count == 0)
                return 0;

            // Copy keys to avoid modifying the set while iterating
            var keys = new List<MessageLangKey>(set);
            int removed = 0;
            foreach (var key in keys)
                if (cache.TryRemove(key, out _))
                    removed++;

            byMessageId.Remove(messageId);
            return removed;
        }

        /// <summary>
        ///     Drop everything (used on identity change, full reset, or scene unload).
        /// </summary>
        public void Clear()
        {
            cache.Clear();
            byMessageId.Clear();
        }

        /// <summary>
        ///     Internal eviction hook keeps secondary index in sync,
        ///     increments local counter, and forwards to external telemetry if provided.
        /// </summary>
        private void OnEvicted(MessageLangKey key, TranslationResult _)
        {
            Evictions++;

            // Keep the index consistent
            if (byMessageId.TryGetValue(key.MessageId, out var set))
            {
                set.Remove(key);
                if (set.Count == 0) byMessageId.Remove(key.MessageId);
            }

            // External telemetry/counters
            onEvictedExternal?.Invoke(key, _);
        }
    }

    /// <summary>
    ///     Struct key used by InMemoryTranslationCache to avoid string-concat keys.
    ///     Combines messageId and target language into a value-type key.
    /// </summary>
    public readonly struct MessageLangKey : IEquatable<MessageLangKey>
    {
        public readonly string MessageId;
        public readonly LanguageCode Lang;

        public MessageLangKey(string messageId, LanguageCode lang)
        {
            MessageId = messageId;
            Lang = lang;
        }

        public bool Equals(MessageLangKey other)
        {
            return MessageId == other.MessageId && Lang.Equals(other.Lang);
        }

        public override bool Equals(object obj)
        {
            return obj is MessageLangKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MessageId, Lang);
        }
    }
}