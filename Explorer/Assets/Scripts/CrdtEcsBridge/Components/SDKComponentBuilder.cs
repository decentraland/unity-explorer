using CrdtEcsBridge.Serialization;
using CrdtEcsBridge.WorldSynchronizer.CommandBufferSynchronizer;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;

namespace CrdtEcsBridge.Components
{
    public struct SDKComponentBuilder<T> where T: class, new()
    {
        private int id;
        internal IComponentSerializer<T> serializer;
        internal IComponentPool<T> pool;

        public static SDKComponentBuilder<T> Create(int id) =>
            new () { id = id };

        public readonly SDKComponentBridge Build()
        {
            serializer.EnsureNotNull();
            pool.EnsureNotNull();

            return new SDKComponentBridge(
                id,
                serializer,
                typeof(T),
                pool,
                new LogSDKComponentCommandBufferSynchronizer<T>(
                    new SDKComponentCommandBufferSynchronizer<T>(pool),
                    ReportHub.WithReport(ReportCategory.CRDT_ECS_BRIDGE).Log
                )
            );
        }
    }
}
