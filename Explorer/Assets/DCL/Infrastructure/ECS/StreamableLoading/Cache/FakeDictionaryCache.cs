using System;
using System.Collections;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    public class FakeDictionaryCache<TAsset> : IDictionary<string, TAsset>
    {
        public TAsset this[string key]
        {
            get => throw new NotImplementedException();

            set => throw new NotImplementedException();
        }

        public ICollection<string> Keys { get; }
        public ICollection<TAsset> Values { get; }

        public int Count { get; }
        public bool IsReadOnly { get; }

        public void Add(string key, TAsset value) { }

        public bool ContainsKey(string key) =>
            false;

        public bool Remove(string key) =>
            false;

        public bool TryGetValue(string key, out TAsset value)
        {
            value = default(TAsset);
            return false;
        }

        public IEnumerator<KeyValuePair<string, TAsset>> GetEnumerator() =>
            throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public void Add(KeyValuePair<string, TAsset> item) { }

        public void Clear() { }

        public bool Contains(KeyValuePair<string, TAsset> item) =>
            false;

        public void CopyTo(KeyValuePair<string, TAsset>[] array, int arrayIndex) { }

        public bool Remove(KeyValuePair<string, TAsset> item) =>
            false;
    }
}
