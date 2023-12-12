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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SyncAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            lock (dictionary) { dictionary.Add(key, value); }
        }

        public static bool SyncTryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            lock (dictionary) { return dictionary.TryGetValue(key, out value); }
        }
    }
}
