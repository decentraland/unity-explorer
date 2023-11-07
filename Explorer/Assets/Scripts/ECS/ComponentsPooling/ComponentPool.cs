using System;
using UnityEngine.Pool;
using Utility.ThreadSafePool;

namespace ECS.ComponentsPooling
{
    public class ComponentPool<T> : IComponentPool<T> where T: class, new()
    {
        /// <summary>
        ///     It is not thread-safe
        /// </summary>
        private readonly ThreadSafeObjectPool<T> objectPool;

        public int CountInactive => objectPool.CountInactive;

        public ComponentPool(Action<T> onGet = null, Action<T> onRelease = null, int defaultCapacity = 10, int maxSize = 10000)
        {
            objectPool = new ThreadSafeObjectPool<T>(() => new T(), actionOnGet: onGet, actionOnRelease: onRelease, collectionCheck: false,
                defaultCapacity: defaultCapacity, maxSize: maxSize);
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
    }
}
