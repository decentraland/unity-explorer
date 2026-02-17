using System.Collections.Generic;

namespace Utility.Multithreading
{
    // WebGL friendly implementation of concurrent dictionary
    public class DCLConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
#if !UNITY_WEBGL
        private readonly System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> Inner;
#else
        private readonly Dictionary<TKey, TValue> Inner;
#endif
        public TValue this[TKey key]
        {
            get => Inner[key];
            set => Inner[key] = value;
        }

        public int Count => Inner.Count;

        public bool IsReadOnly => false;

        public ICollection<TKey> Keys => Inner.Keys;

        public ICollection<TValue> Values => Inner.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Inner.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Inner.Values;

        public DCLConcurrentDictionary()
        {
            Inner = new ();
        }

        public DCLConcurrentDictionary(System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        {
            Inner = new (comparer);
        }

        public void Add(TKey key, TValue value)
        {
#if !UNITY_WEBGL
            Inner.AddOrGet(key, value);
#else
            Inner.Add(key, value);
#endif
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
#if !UNITY_WEBGL
            Inner.AddOrGet(item.Key, item.Value);
#else
            Inner.Add(item.Key, item.Value);
#endif
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)Inner).Contains(item);

        public bool ContainsKey(TKey key) =>
            Inner.ContainsKey(key);

        public bool Remove(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)Inner).Remove(item);

        public bool Remove(TKey key) =>
            Inner.Remove(key, out TValue _);

        public bool TryRemove(TKey key, out TValue value)
        {
#if !UNITY_WEBGL
            return Inner.TryRemove(key, out value);
#else
            if (Inner.TryGetValue(key, out value))
            {
                return Inner.Remove(key);
            }

            value = default;
            return false;
#endif
        }

        public void Clear()
        {
            Inner.Clear();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)Inner).CopyTo(array, arrayIndex);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return Inner.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            Inner.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            Inner.GetEnumerator();
    }
}
