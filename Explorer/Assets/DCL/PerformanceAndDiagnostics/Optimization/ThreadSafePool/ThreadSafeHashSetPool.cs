using System.Collections.Generic;

namespace DCL.Optimization.ThreadSafePool
{
    public class ThreadSafeHashSetPool<TItem> : ThreadSafeCollectionPool<HashSet<TItem>, TItem>
    {
        public ThreadSafeHashSetPool(int initialCapacity, int poolCapacity) : base(initialCapacity, poolCapacity, capacity => new HashSet<TItem>(capacity)) { }
    }
}
