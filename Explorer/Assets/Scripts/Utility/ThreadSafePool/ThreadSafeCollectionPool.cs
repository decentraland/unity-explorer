using System;
using System.Collections.Generic;

namespace Utility.ThreadSafePool
{
    public abstract class ThreadSafeCollectionPool<TCollection, TItem> : ThreadSafeObjectPool<TCollection> where TCollection: class, ICollection<TItem>, new()
    {
        /// <summary>
        /// Shared Collection Pool with default capacity
        /// </summary>
        public static readonly ThreadSafeObjectPool<TCollection> Shared = new (() => new TCollection(), actionOnRelease: c => c.Clear());

        public ThreadSafeCollectionPool(int initialCapacity, int poolCapacity, Func<int, TCollection> createFunc)
            : base(() => createFunc(initialCapacity), actionOnRelease: c => c.Clear(), defaultCapacity: poolCapacity, collectionCheck: false) { }
    }
}
