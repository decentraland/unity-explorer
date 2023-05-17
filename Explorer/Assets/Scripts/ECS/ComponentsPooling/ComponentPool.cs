using System;
using UnityEngine.Pool;
using Utility.ThreadSafePool;

namespace ECS.ComponentsPooling
{
    public class ComponentPool<T> : IComponentPool<T> where T: class, new()
    {
        private readonly ThreadSafeObjectPool<T> objectPool;

        public ComponentPool(Action<T> onGet = null, Action<T> onRelease = null)
        {
            objectPool = new ThreadSafeObjectPool<T>(() => new T(), actionOnGet: onGet, actionOnRelease: onRelease, collectionCheck: false);
        }

        public T Get() =>
            objectPool.Get();

        public PooledObject<T> Get(out T v) =>
            objectPool.Get(out v);

        public void Release(T component) =>
            objectPool.Release(component);

        public void Clear() =>
            objectPool.Clear();

        public int CountInactive => objectPool.CountInactive;

        public void Dispose()
        {
            objectPool.Clear();
        }
    }
}
