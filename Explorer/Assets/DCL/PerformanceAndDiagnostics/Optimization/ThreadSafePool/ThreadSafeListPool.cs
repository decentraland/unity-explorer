using System.Collections.Generic;

namespace DCL.Optimization.ThreadSafePool
{
    public class ThreadSafeListPool<TItem> : ThreadSafeCollectionPool<List<TItem>, TItem>
    {
        public ThreadSafeListPool(int initialCapacity, int poolCapacity) : base(initialCapacity, poolCapacity, capacity => new List<TItem>(capacity)) { }
    }
}
