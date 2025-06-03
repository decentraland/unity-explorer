using System.Collections.Generic;

namespace DCL.Optimization.ThreadSafePool
{
    public class ThreadSafeDictionaryPool<TKey, TValue> : ThreadSafeCollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>
    {
        public ThreadSafeDictionaryPool(int initialCapacity, int poolCapacity, IEqualityComparer<TKey>? equalityComparer = null)
            : base(initialCapacity, poolCapacity, capacity => new Dictionary<TKey, TValue>(capacity, equalityComparer)) { }
    }
}
