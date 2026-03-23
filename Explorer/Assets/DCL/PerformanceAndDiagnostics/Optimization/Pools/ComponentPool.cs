using DCL.Optimization.ThreadSafePool;
using System;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    /// <summary>
    /// Entry point for Thread-safe pools dedicated to SDK/ECS components
    /// </summary>
    public static class ComponentPool
    {
        public class WithDefaultCtor<T> : WithFactory<T> where T : class, new()
        {
            public WithDefaultCtor(Action<T> onGet = null, Action<T> onRelease = null, Action<T> actionOnDestroy = null, int defaultCapacity = 10, int maxSize = 10000) :
                base(static () => new T(), onGet, onRelease, actionOnDestroy, defaultCapacity, maxSize)
            {
            }
        }

        public class WithFactory<T> : IComponentPool<T> where T : class
        {
            /// <summary>
            ///     It is not thread-safe
            /// </summary>
            private readonly ThreadSafeObjectPool<T> objectPool;

            public int CountInactive => objectPool.CountInactive;

            public WithFactory(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> actionOnDestroy = null, int defaultCapacity = 10, int maxSize = 10000)
            {
                objectPool = new ThreadSafeObjectPool<T>(createFunc, actionOnGet: onGet, actionOnRelease: onRelease, actionOnDestroy: actionOnDestroy, collectionCheck: PoolConstants.CHECK_COLLECTIONS,
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

            public void ClearThrottled(int maxUnloadAmount) =>
                objectPool.ClearThrottled(maxUnloadAmount);
        }
    }
}
