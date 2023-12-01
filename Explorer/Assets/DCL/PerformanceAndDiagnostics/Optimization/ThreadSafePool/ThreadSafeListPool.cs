using System.Collections.Generic;

namespace Utility.ThreadSafePool
{
    public class ThreadSafeListPool<TItem> : ThreadSafeCollectionPool<List<TItem>, TItem>
    {
        public ThreadSafeListPool(int initialCapacity, int poolCapacity) : base(initialCapacity, poolCapacity, capacity => new List<TItem>(capacity)) { }
    }
}
