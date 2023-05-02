using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    /// Entry point to merry ECS and SDK Components after CRDT synchronization
    /// </summary>
    public class CrdtWorldSynchronizer : ICrdtWorldSynchronizer
    {
        private readonly World world;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;

        private bool commandBufferRented;

        private readonly Arch.Core.CommandBuffer.CommandBuffer reusableCommandBuffer;

        public CrdtWorldSynchronizer(World world, ISDKComponentsRegistry sdkComponentsRegistry)
        {
            this.world = world;
            entitiesMap = new Dictionary<CRDTEntity, Entity>();

            reusableCommandBuffer = new Arch.Core.CommandBuffer.CommandBuffer(world);
            this.sdkComponentsRegistry = sdkComponentsRegistry;
        }

        public IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap => entitiesMap;

        public WorldSyncCommandBuffer GetSyncCommandBuffer()
        {
            if (commandBufferRented)
                throw new InvalidOperationException("Command buffer is already rented");

            commandBufferRented = true;
            return new WorldSyncCommandBuffer(sdkComponentsRegistry);
        }

        public void ApplySyncCommandBuffer(WorldSyncCommandBuffer syncCommandBuffer)
        {
            syncCommandBuffer.Apply(world, reusableCommandBuffer, entitiesMap);
            commandBufferRented = false;
        }
    }
}
