using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Utility.Pool
{
    /// <summary>
    ///     Provides functionality similarly <see cref="ObjectPool{T}" /> in an instance manner (unlike static <see cref="ListPool{T}" />)
    /// </summary>
    public class ListObjectPool<T> : ObjectPool<List<T>>
    {
        public ListObjectPool(
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

        public Scope AutoScope() =>
            new (Get(), this);

        public struct Scope : IDisposable
        {
            private readonly ListObjectPool<T> pool;
            public List<T> List { get; private set; }

            internal Scope(List<T> list, ListObjectPool<T> pool)
            {
                this.pool = pool;
                List = list;
            }

            public void Dispose()
            {
                pool.Release(List);
                List = null;
            }
        }
    }
}
