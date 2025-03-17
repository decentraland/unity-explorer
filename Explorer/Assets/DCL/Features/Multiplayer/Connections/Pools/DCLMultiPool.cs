using DCL.Optimization.ThreadSafePool;
using LiveKit.Internal.FFIClients.Pools;
using System;
using System.Collections.Concurrent;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Connections.Pools
{
    public class DCLMultiPool : IMultiPool
    {
        private readonly ConcurrentDictionary<Type, IObjectPool<object>> pools = new ();

        public T Get<T>() where T: class, new()
        {
            return (T)Pool<T>().Get()!;
        }

        public void Release<T>(T poolObject) where T: class, new()
        {
            Pool<T>().Release(poolObject);
        }

        private IObjectPool<object> Pool<T>() where T: class, new()
        {
            var type = typeof(T);

            if (!pools.TryGetValue(type, out var pool)) { pool = pools[type] = new ThreadSafeObjectPool<object>(() => new T()); }

            return pool!;
        }
    }
}
