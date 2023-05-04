using System;
using UnityEngine.Pool;

namespace Utility.ThreadSafePool
{
    /// <summary>
    /// Represents a pool that can be shared between multiple threads
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ThreadSafeObjectPool<T> : IObjectPool<T> where T: class
    {
        private IObjectPool<T> objectPoolImplementation;

        public ThreadSafeObjectPool(Func<T> createFunc, Action<T> actionOnGet = null, Action<T> actionOnRelease = null, Action<T> actionOnDestroy = null, bool collectionCheck = true,
            int defaultCapacity = 10, int maxSize = 10000)
        {
            objectPoolImplementation = new ObjectPool<T>(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck, defaultCapacity, maxSize);
        }

        public T Get()
        {
            lock (objectPoolImplementation) { return objectPoolImplementation.Get(); }
        }

        public PooledObject<T> Get(out T v)
        {
            lock (objectPoolImplementation) { return objectPoolImplementation.Get(out v); }
        }

        public void Release(T element)
        {
            lock (objectPoolImplementation) { objectPoolImplementation.Release(element); }
        }

        public void Clear()
        {
            lock (objectPoolImplementation) { objectPoolImplementation.Clear(); }
        }

        public int CountInactive
        {
            get
            {
                lock (objectPoolImplementation) { return objectPoolImplementation.CountInactive; }
            }
        }
    }
}
