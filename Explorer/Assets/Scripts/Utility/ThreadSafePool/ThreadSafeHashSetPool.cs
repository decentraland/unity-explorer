using System.Collections.Generic;

namespace Utility.ThreadSafePool
{
    public class ThreadSafeHashSetPool<TItem> : ThreadSafeCollectionPool<HashSet<TItem>, TItem>
    {
        public ThreadSafeHashSetPool(int initialCapacity, int poolCapacity) : base(initialCapacity, poolCapacity, capacity => new HashSet<TItem>(capacity)) { }
    }
}
