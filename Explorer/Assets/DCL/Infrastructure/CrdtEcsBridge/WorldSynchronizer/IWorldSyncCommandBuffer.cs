using Arch.Buffer;
using Arch.Core;
using CRDT;
using CRDT.Protocol;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    ///     Merges CRDT Messages to their final state
    ///     to execute deserialize and execute them only once in the ECS World.
    ///     Can't be executed concurrently
    /// </summary>
    public interface IWorldSyncCommandBuffer : IDisposable
    {
        /// <summary>
        ///     Add messages in the order they are processed by CRDT
        ///     This sync function is for ECS syncing and it heavily relies on the proper result from the CRDT Protocol
        /// </summary>
        /// <returns>The last status corresponding to the (<see cref="CRDTMessage.EntityId" />, <see cref="CRDTMessage.ComponentId" />)</returns>
        CRDTReconciliationEffect SyncCRDTMessage(in CRDTMessage message, CRDTReconciliationEffect reconciliationEffect);

        /// <summary>
        ///     Resolves the final state of (entity, component) <br />
        ///     Deserializes only the last state of the component <br />
        ///     Should be called from the background thread <br />
        /// </summary>
        void FinalizeAndDeserialize();

        /// <summary>
        ///     Applies deserialized changes to the world.
        ///     Must be called on the thread where World is running
        /// </summary>
        void Apply(World world, PersistentCommandBuffer commandBuffer, Dictionary<CRDTEntity, Entity> entitiesMap);
    }
}
