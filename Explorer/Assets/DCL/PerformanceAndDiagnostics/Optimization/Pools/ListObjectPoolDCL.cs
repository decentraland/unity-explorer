using System;
using System.Collections.Generic;

namespace DCL.PerformanceAndDiagnostics.Optimization.Pools
{
    /// <summary>
    ///     Provides functionality similarly <see cref="ObjectPoolDCL{T}" /> in an instance manner (unlike static <see cref="ListPool{T}" />)
    /// </summary>
    public class ListObjectPoolDCL<T> : ObjectPoolDCL<List<T>>
    {
        public ListObjectPoolDCL(
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
