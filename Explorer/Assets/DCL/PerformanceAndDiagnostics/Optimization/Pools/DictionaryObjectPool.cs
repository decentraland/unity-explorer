using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    /// <summary>
    ///     Provides functionality similarly <see cref="ObjectPool{T}" /> in an instance manner (unlike static <see cref="DictionaryPool{TKey, TValue}" />)
    /// </summary>
    public class DictionaryObjectPool<TKey, TValue> : ObjectPool<Dictionary<TKey, TValue>>
    {
        public DictionaryObjectPool(
            Action<Dictionary<TKey, TValue>> actionOnGet = null,
            Action<Dictionary<TKey, TValue>> actionOnDestroy = null,
            bool collectionCheck = PoolConstants.CHECK_COLLECTIONS,
            int dictionaryInstanceDefaultCapacity = 100,
            int defaultCapacity = 10,
            int maxSize = 10000) : base(() => new Dictionary<TKey, TValue>(dictionaryInstanceDefaultCapacity),
            actionOnGet,
            l => l.Clear(),
            actionOnDestroy,
            collectionCheck,
            defaultCapacity,
            maxSize) { }
    }
}
