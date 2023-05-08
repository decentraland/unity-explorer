using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    /// Entry point to merry ECS and SDK Components after CRDT synchronization
    /// </summary>
    public class CRDTWorldSynchronizer : ICRDTWorldSynchronizer
    {
        private const int BUFFER_POOLS_CAPACITY = 64;
        private const int RENT_WAIT_TIMEOUT = 5000;

        private readonly World world;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;

        private readonly Arch.Core.CommandBuffer.CommandBuffer reusableCommandBuffer;

        // We can't use a mutex as it must be acquired and released by the same thread
        // and it is not guaranteed as we use thread pools (in the most cases different threads are used for getting and applying command buffers)
        private readonly SemaphoreSlim semaphore = new (1, 1);

        public CRDTWorldSynchronizer(World world, ISDKComponentsRegistry sdkComponentsRegistry)
        {
            this.world = world;
            entitiesMap = new Dictionary<CRDTEntity, Entity>();

            reusableCommandBuffer = new Arch.Core.CommandBuffer.CommandBuffer(world, BUFFER_POOLS_CAPACITY);
            this.sdkComponentsRegistry = sdkComponentsRegistry;
        }

        public IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap => entitiesMap;

        public IWorldSyncCommandBuffer GetSyncCommandBuffer()
        {
            if (!semaphore.Wait(RENT_WAIT_TIMEOUT))
                throw new TimeoutException("Rent Wait Timeout: Couldn't rent command buffer");

            return new WorldSyncCommandBuffer(sdkComponentsRegistry);
        }

        public void ApplySyncCommandBuffer(IWorldSyncCommandBuffer syncCommandBuffer)
        {
            syncCommandBuffer.Apply(world, reusableCommandBuffer, entitiesMap);
            semaphore.Release();
        }

        public void Dispose()
        {
            reusableCommandBuffer.Dispose();
            semaphore.Dispose();
        }
    }
}
