using CrdtEcsBridge.Serialization;
using CrdtEcsBridge.WorldSynchronizer.CommandBuffer;
using DCL.Optimization.Pools;
using System;

namespace CrdtEcsBridge.Components
{
    public class SDKComponentBridge
    {
        public readonly int Id;
        public readonly Type ComponentType;
        public readonly IComponentSerializer Serializer;
        public readonly IComponentPool Pool;
        public readonly SDKComponentCommandBufferSynchronizer CommandBufferSynchronizer;

        internal SDKComponentBridge(int id, IComponentSerializer serializer, Type componentType, IComponentPool pool, SDKComponentCommandBufferSynchronizer commandBufferSynchronizer)
        {
            Id = id;
            Serializer = serializer;
            ComponentType = componentType;
            Pool = pool;
            CommandBufferSynchronizer = commandBufferSynchronizer;
        }
    }
}
