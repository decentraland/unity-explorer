using System.Collections.Generic;

namespace Utility.ThreadSafePool
{
    public class ThreadSafeDictionaryPool<TKey, TValue> : ThreadSafeCollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>
    {
        public ThreadSafeDictionaryPool(int initialCapacity, int poolCapacity) : base(initialCapacity, poolCapacity, capacity => new Dictionary<TKey, TValue>(capacity)) { }
    }
}
