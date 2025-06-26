using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    /// <summary>
    ///     Provides functionality similarly <see cref="ExtendedObjectPool{T}" /> in an instance manner (unlike static <see cref="ListPool{T}" />)
    /// </summary>
    public class ListExtendedObjectPool<T> : ExtendedObjectPool<List<T>>
    {
        public ListExtendedObjectPool(
            Action<List<T>> actionOnGet = null,
            Action<List<T>> actionOnDestroy = null,
            bool collectionCheck = true,
            int listInstanceDefaultCapacity = 100,
            int defaultCapacity = 10,
            int maxSize = 10000) : base(() => new List<T>(listInstanceDefaultCapacity),
            actionOnGet,
            l => l.Clear(),
            actionOnDestroy,
            collectionCheck,
            defaultCapacity,
            maxSize) { }
    }
}
