using System;
using System.Collections;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    public class FakeDictionaryCache<TKey, TAsset> : IDictionary<TKey, TAsset>
    {
        public TAsset this[TKey key]
        {
            get => throw new NotImplementedException();

            set => throw new NotImplementedException();
        }

        public ICollection<TKey> Keys { get; }
        public ICollection<TAsset> Values { get; }

        public int Count { get; }
        public bool IsReadOnly { get; }

        public void Add(TKey key, TAsset value) { }

        public bool ContainsKey(TKey key) =>
            false;

        public bool Remove(TKey key) =>
            false;

        public bool TryGetValue(TKey key, out TAsset value)
        {
            value = default(TAsset);
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TAsset>> GetEnumerator() =>
            throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public void Add(KeyValuePair<TKey, TAsset> item) { }

        public void Clear() { }

        public bool Contains(KeyValuePair<TKey, TAsset> item) =>
            false;

        public void CopyTo(KeyValuePair<TKey, TAsset>[] array, int arrayIndex) { }

        public bool Remove(KeyValuePair<TKey, TAsset> item) =>
            false;
    }
}
