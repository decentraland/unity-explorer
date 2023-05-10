using Arch.CommandBuffer;
using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    /// Entry point to merry ECS and SDK Components after CRDT synchronization
    /// </summary>
    public class CRDTWorldSynchronizer : ICRDTWorldSynchronizer
    {
        private const int BUFFER_POOLS_CAPACITY = 64;

        private readonly World world;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly IEntityFactory entityFactory;

        private readonly PersistentCommandBuffer reusableCommandBuffer;

        private bool disposed;

        // We can't use a mutex as it must be acquired and released by the same thread
        // and it is not guaranteed as we use thread pools (in the most cases different threads are used for getting and applying command buffers)
        private readonly SemaphoreSlim semaphore = new (1, 1);

        public CRDTWorldSynchronizer(World world, ISDKComponentsRegistry sdkComponentsRegistry, IEntityFactory entityFactory,
            int initialEntitiesCapacity = 1000)
        {
            this.world = world;
            entitiesMap = new Dictionary<CRDTEntity, Entity>(initialEntitiesCapacity, CRDTEntityComparer.INSTANCE);

            reusableCommandBuffer = new PersistentCommandBuffer(world, BUFFER_POOLS_CAPACITY);
            this.sdkComponentsRegistry = sdkComponentsRegistry;
            this.entityFactory = entityFactory;
        }

        public IReadOnlyDictionary<CRDTEntity, Entity> EntitiesMap => entitiesMap;

        public IWorldSyncCommandBuffer GetSyncCommandBuffer()
        {
            // Timeout in Editor will fire up if the pause is enabled
            // So just wait while on pause
            MultithreadingUtility.WaitWhileOnPause();

            const int RENT_WAIT_TIMEOUT = 5000;

            if (!semaphore.Wait(RENT_WAIT_TIMEOUT))
                throw new TimeoutException("Rent Wait Timeout: Couldn't rent command buffer");

            return new WorldSyncCommandBuffer(sdkComponentsRegistry, entityFactory);
        }

        public void ApplySyncCommandBuffer(IWorldSyncCommandBuffer syncCommandBuffer)
        {
            if (disposed) return;

            syncCommandBuffer.Apply(world, reusableCommandBuffer, entitiesMap);
            semaphore.Release();
        }

        public void Dispose()
        {
            reusableCommandBuffer.Dispose();
            semaphore.Dispose();
            disposed = true;
        }
    }
}
