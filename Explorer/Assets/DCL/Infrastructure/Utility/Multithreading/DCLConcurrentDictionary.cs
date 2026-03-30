using System.Collections.Generic;

namespace Utility.Multithreading
{
    /// <summary>
    ///     WebGL-compatible drop-in replacement for <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />.
    ///     On non-WebGL platforms the real <c>ConcurrentDictionary</c> is used for thread-safe access.
    ///     On WebGL (single-threaded) a plain <see cref="Dictionary{TKey,TValue}" /> is used instead, since
    ///     WebGL has no OS threads and the locking overhead of <c>ConcurrentDictionary</c> is unnecessary.
    ///     Implements both <see cref="IDictionary{TKey,TValue}" /> and <see cref="IReadOnlyDictionary{TKey,TValue}" />
    ///     so it can be used wherever either interface is required.
    /// </summary>
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
#if UNITY_WEBGL
            Inner.Add(key, value);
#else
            Inner.TryAdd(key, value);
#endif
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
#if UNITY_WEBGL
            Inner.Add(item.Key, item.Value);
#else
            Inner.TryAdd(item.Key, item.Value);
#endif
        }

        public bool TryAdd(TKey key, TValue value)
        {
#if UNITY_WEBGL
            if (Inner.ContainsKey(key))
                return false;

            Inner.Add(key, value);
            return true;
#else
            return Inner.TryAdd(key, value);
#endif
        }

        public TValue GetValueOrDefault(TKey key)
        {
            return Inner.TryGetValue(key, out TValue value) ? value : default;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)Inner).Contains(item);

        public bool ContainsKey(TKey key) =>
            Inner.ContainsKey(key);

        public bool Remove(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)Inner).Remove(item);


        public bool Remove(TKey key)
        {
#if UNITY_WEBGL
            return Inner.Remove(key);
#else
            return Inner.TryRemove(key, out _);
#endif
        }
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
