using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Utility
{
    public static class CollectionsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SyncRemove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            lock (dictionary) { dictionary.Remove(key); }
        }

        public static void SyncAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            lock (dictionary) { dictionary.Add(key, value); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SyncTryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            lock (dictionary) { return dictionary.TryAdd(key, value); }
        }

        public static bool SyncTryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            lock (dictionary) { return dictionary.TryGetValue(key, out value); }
        }

        public static void AlignWithDictionary<TSource, TKey, TValue>(this IReadOnlyList<TSource> source, IDictionary<TKey, TValue> dictionary, Func<TSource, TKey> keySelector, Func<TSource, TValue> elementSelector)
        {
            dictionary.Clear();

            for (var i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                dictionary.Add(keySelector(element), elementSelector(element));
            }
        }
    }
}
