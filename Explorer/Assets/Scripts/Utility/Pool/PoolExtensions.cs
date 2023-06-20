using System;
using UnityEngine.Pool;

namespace Utility.Pool
{
    public static class PoolExtensions
    {
        public static Scope<TElement> AutoScope<TElement>(this IObjectPool<TElement> pool) where TElement: class =>
            new (pool.Get(), pool);

        public struct Scope<TElement> : IDisposable where TElement: class
        {
            private readonly IObjectPool<TElement> pool;
            public TElement Value { get; private set; }

            internal Scope(TElement value, IObjectPool<TElement> pool)
            {
                this.pool = pool;
                Value = value;
            }

            public void Dispose()
            {
                pool.Release(Value);
                Value = null;
            }
        }
    }
}
