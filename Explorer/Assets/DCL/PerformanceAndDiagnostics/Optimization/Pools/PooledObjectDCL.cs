using System;

namespace DCL.Pools
{
    /// <summary>
    ///     DCL replication of Unity object pooling codebase
    /// </summary>
    public readonly struct PooledObjectDCL<T> : IDisposable where T: class
    {
        private readonly T toReturn;
        private readonly IObjectPoolDCL<T> pool;

        internal PooledObjectDCL(T value, IObjectPoolDCL<T> pool)
        {
            toReturn = value;
            this.pool = pool;
        }

        void IDisposable.Dispose() =>
            pool.Release(toReturn);
    }
}
