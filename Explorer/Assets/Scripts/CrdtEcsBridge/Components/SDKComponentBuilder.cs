using CrdtEcsBridge.Serialization;
using CrdtEcsBridge.WorldSynchronizer.CommandBuffer;
using DCL.Optimization.Pools;

namespace CrdtEcsBridge.Components
{
    public struct SDKComponentBuilder<T> where T: class, new()
    {
        private int id;
        internal IComponentSerializer<T> serializer;
        internal IComponentPool<T> pool;

        public static SDKComponentBuilder<T> Create(int id) =>
            new () { id = id };

        public SDKComponentBridge Build() =>
            new (id, serializer, typeof(T), pool, new SDKComponentCommandBufferSynchronizer<T>(pool));
    }
}
