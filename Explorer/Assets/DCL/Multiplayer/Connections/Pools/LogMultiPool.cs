using LiveKit.Internal.FFIClients.Pools;
using System;

namespace DCL.Multiplayer.Connections.Pools
{
    public class LogMultiPool : IMultiPool
    {
        private readonly IMultiPool multiPool;
        private readonly Action<string> log;

        public LogMultiPool(IMultiPool multiPool, Action<string> log)
        {
            this.multiPool = multiPool;
            this.log = log;
        }

        public T Get<T>() where T: class, new()
        {
            log($"request for object: {typeof(T).FullName}");
            return multiPool.Get<T>();
        }

        public void Release<T>(T poolObject) where T: class, new()
        {
            log($"releasing object: {typeof(T).FullName}");
            multiPool.Release(poolObject);
        }
    }
}
