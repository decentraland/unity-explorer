using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;

namespace DCL.Optimization.ThreadSafePool
{
    public abstract class ThreadSafeCollectionPool<TCollection, TItem> : ThreadSafeObjectPool<TCollection> where TCollection: class, ICollection<TItem>, new()
    {
        /// <summary>
        ///     Shared Collection Pool with default capacity
        /// </summary>
        public static readonly ThreadSafeObjectPool<TCollection> SHARED = new (() => new TCollection(), actionOnRelease: c => c.Clear());

        protected ThreadSafeCollectionPool(int initialCapacity, int poolCapacity, Func<int, TCollection> createFunc)
            : base(() => createFunc(initialCapacity), actionOnRelease: c => c.Clear(), defaultCapacity: poolCapacity, collectionCheck: PoolConstants.CHECK_COLLECTIONS) { }
    }
}
