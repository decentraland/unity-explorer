using DCL.Optimization.ThreadSafePool;
using System;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    /// <summary>
    /// Basically it's wrap upon ThreadSafeObjectPool to match IComponentPool interface
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ThreadSafeComponentPool<T> : IComponentPool<T> where T: class
    {
        private readonly ThreadSafeObjectPool<T> objectPool;

        public int CountInactive => objectPool.CountInactive;

        public ThreadSafeComponentPool(Func<T> createFunc) : this(new ThreadSafeObjectPool<T>(createFunc)) { }

        public ThreadSafeComponentPool(ThreadSafeObjectPool<T> objectPool)
        {
            this.objectPool = objectPool;
        }

        public void Dispose()
        {
            objectPool.Clear();
        }

        public T Get() =>
            objectPool.Get();

        public PooledObject<T> Get(out T v) =>
            objectPool.Get(out v);

        public void Release(T component) =>
            objectPool.Release(component);

        public void Clear() =>
            objectPool.Clear();

        public void ClearThrottled(int maxUnloadAmount) =>
            objectPool.ClearThrottled(maxUnloadAmount);
    }
}
