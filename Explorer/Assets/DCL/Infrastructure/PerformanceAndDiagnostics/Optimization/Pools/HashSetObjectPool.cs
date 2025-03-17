using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    /// <summary>
    ///     Provides functionality similarly <see cref="ObjectPool{T}" /> in an instance manner (unlike static <see cref="HashSetPool{T}" />)
    /// </summary>
    public class HashSetObjectPool<T> : ObjectPool<HashSet<T>>
    {
        public HashSetObjectPool(
            Action<HashSet<T>> actionOnGet = null,
            Action<HashSet<T>> actionOnDestroy = null,
            bool collectionCheck = true,
            int hashsetInstanceDefaultCapacity = 100,
            int defaultCapacity = 10,
            int maxSize = 10000) : base(() => new HashSet<T>(hashsetInstanceDefaultCapacity),
            actionOnGet,
            l => l.Clear(),
            actionOnDestroy,
            collectionCheck,
            defaultCapacity,
            maxSize) { }
    }
}
