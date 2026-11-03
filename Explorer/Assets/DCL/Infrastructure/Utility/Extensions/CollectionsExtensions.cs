using DCL.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Utility
{
    public static class CollectionsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SyncRemove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            lock (dictionary) { return dictionary.Remove(key); }
        }

        public static void SyncAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            bool added;
            lock (dictionary) { added = dictionary.TryAdd(key, value); }

            if (!added)
                ReportHub.LogError(ReportCategory.UNSPECIFIED, $"Key {key} is already present in the dictionary");
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
    }
}
