using Arch.Core;
using Collections.Pooled;
using CRDT;
using CRDT.Protocol;
using CrdtEcsBridge.Components;
using ECS.LifeCycle.Components;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrdtEcsBridge.WorldSynchronizer
{
    /// <summary>
    /// Merges CRDT Messages to their final state
    /// to execute deserialize and execute them only once in the ECS World.
    /// Can't be executed concurrently
    /// </summary>
    public class WorldSyncCommandBuffer : IDisposable
    {
        /// <summary>
        /// Represents the final state of the operation on (entity, component)
        /// </summary>
        private static readonly Dictionary<ReconciliationState, CRDTReconciliationEffect> MERGE_MATRIX =
            new ()
            {
                // if the first op is "ComponentAdded" then the component didn't exists so it should be added if not deleted
                { (CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentAdded), CRDTReconciliationEffect.ComponentAdded },
                { (CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentModified), CRDTReconciliationEffect.ComponentAdded },

                // if deleted then nothing to do as it didn't exist before
                { (CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentDeleted), CRDTReconciliationEffect.NoChanges },

                // if component already existed then if the component still exists it should be modified
                { (CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentAdded), CRDTReconciliationEffect.NoChanges },
                { (CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified), CRDTReconciliationEffect.NoChanges },

                // if it was deleted then delete from World
                { (CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentDeleted), CRDTReconciliationEffect.ComponentDeleted },

                // if component already existed then if the component still exists it should be modified
                { (CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentModified), CRDTReconciliationEffect.NoChanges },
                { (CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentModified), CRDTReconciliationEffect.NoChanges },

                // if it was deleted then delete from World
                { (CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentDeleted), CRDTReconciliationEffect.ComponentDeleted },
            };

        private readonly struct ReconciliationState : IEquatable<ReconciliationState>
        {
            /// <summary>
            /// Represents the status of the (entity, component) at the start of the batch
            /// </summary>
            public readonly CRDTReconciliationEffect First;

            /// <summary>
            /// Represents the most recent command
            /// </summary>
            public readonly CRDTReconciliationEffect Last;

            internal ReconciliationState(CRDTReconciliationEffect first, CRDTReconciliationEffect last)
            {
                First = first;
                Last = last;
            }

            public bool Equals(ReconciliationState other) =>
                First == other.First && Last == other.Last;

            public override bool Equals(object obj) =>
                obj is ReconciliationState other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine((int)First, (int)Last);

            public static implicit operator ReconciliationState((CRDTReconciliationEffect, CRDTReconciliationEffect) tuple) =>
                new (tuple.Item1, tuple.Item2);
        }

        // TODO pooling
        private class BatchState
        {
            internal CRDTMessage crdtMessage;
            internal ReconciliationState reconciliationState;
            internal SDKComponentBridge sdkComponentBridge;

            internal object deserializationTarget;

            public BatchState(CRDTMessage crdtMessage, ReconciliationState reconciliationState, SDKComponentBridge sdkComponentBridge,
                object deserializationTarget)
            {
                this.crdtMessage = crdtMessage;
                this.reconciliationState = reconciliationState;
                this.sdkComponentBridge = sdkComponentBridge;
                this.deserializationTarget = deserializationTarget;
            }
        }

        private readonly PooledDictionary<CRDTEntity, PooledDictionary<int, BatchState>> batchStates;
        private readonly PooledList<CRDTEntity> deletedEntities;

        private readonly ISDKComponentsRegistry sdkComponentsRegistry;

        private bool finalized;

        /// <summary>
        /// Can't contain a public ctor as should be instantiated within the assembly
        /// </summary>
        internal WorldSyncCommandBuffer(ISDKComponentsRegistry componentsRegistry)
        {
            batchStates = new PooledDictionary<CRDTEntity, PooledDictionary<int, BatchState>>();
            deletedEntities = new PooledList<CRDTEntity>();

            this.sdkComponentsRegistry = componentsRegistry;
        }

        /// <summary>
        /// Add messages in the order they are processed by CRDT
        /// This sync function is for ECS syncing and it heavily relies on the proper result from the CRDT Protocol
        /// </summary>
        public void SyncCRDTMessage(in CRDTMessage message, CRDTReconciliationEffect reconciliationEffect)
        {
            if (finalized)
                throw new InvalidOperationException($"{nameof(WorldSyncCommandBuffer)} is already finalized and can't be modified anymore");

            switch (reconciliationEffect)
            {
                // After Entity is processed by CRDT it can't revive so it can't appear in the list twice
                case CRDTReconciliationEffect.EntityDeleted:
                    deletedEntities.Add(message.EntityId);

                    // Just delete the batch
                    // CRDT guarantees that once entity is deleted no more components will be added to it (it can't revive)
                    if (batchStates.TryGetValue(message.EntityId, out var componentsBatch))
                    {
                        componentsBatch.Dispose();
                        batchStates.Remove(message.EntityId);
                    }

                    break;
                case CRDTReconciliationEffect.ComponentAdded:
                case CRDTReconciliationEffect.ComponentDeleted:
                case CRDTReconciliationEffect.ComponentModified:

                    if (!sdkComponentsRegistry.TryGet(message.ComponentId, out var sdkComponentBridge))
                    {
                        Debug.LogWarning($"SDK Component {message.ComponentId} is not registered");
                        return;
                    }

                    // Store the first and the last result
                    bool componentBatchExists;

                    if ((componentBatchExists = batchStates.TryGetValue(message.EntityId, out componentsBatch))
                        && componentsBatch.TryGetValue(message.ComponentId, out var state))
                    {
                        // take the first one, override the last one
                        state.reconciliationState = new ReconciliationState(state.reconciliationState.First, reconciliationEffect);
                    }
                    else
                    {
                        if (!componentBatchExists)
                            componentsBatch = batchStates[message.EntityId] = new PooledDictionary<int, BatchState>();

                        // the first and the last are the same
                        // take the component from the pool
                        componentsBatch[message.ComponentId] = new BatchState(message, new ReconciliationState(reconciliationEffect, reconciliationEffect), sdkComponentBridge, sdkComponentBridge.Pool.Rent());
                    }

                    break;
            }
        }

        /// <summary>
        /// Resolves the final state of (entity, component) <br/>
        /// Deserializes only the last state of the component <br/>
        /// Should be called from the background thread <br/>
        /// </summary>
        public void FinalizeAndDeserialize()
        {
            // resolve the final state

            foreach (var (entity, componentsBatch) in batchStates)
            {
                // It may contain extra elements (that will be resolves as NoChanges, it is not a problem
                // as we use `ArrayPool` anyway

                foreach (var batchState in componentsBatch.Values)
                {
                    var finalState = MERGE_MATRIX[batchState.reconciliationState];

                    // just override with the final state
                    batchState.reconciliationState = new ReconciliationState(finalState, finalState);

                    if (finalState != CRDTReconciliationEffect.ComponentDeleted)
                    {
                        var bridge = batchState.sdkComponentBridge;
                        bridge.Serializer.DeserializeInto(batchState.deserializationTarget, batchState.crdtMessage.Data.Span);
                    }
                }
            }
        }

        /// <summary>
        /// Applies deserialized changes to the world.
        /// Must be called on the thread where World is running
        /// </summary>
        internal void Apply(World world, Arch.Core.CommandBuffer.CommandBuffer commandBuffer, Dictionary<CRDTEntity, Entity> entitiesMap)
        {
            // TODO consider adding "static" satellites such as "ComponentXDirty", it will be addressed later
            // For now it just adds the protobuf component as is

            try
            {
                foreach (var deletedEntity in deletedEntities)
                {
                    if (entitiesMap.TryGetValue(deletedEntity, out var entity))
                    {
                        // let components dispose if the entity was deleted
                        commandBuffer.Add(entity, new DeleteEntityIntention());
                        entitiesMap.Remove(deletedEntity);
                    }
                }

                // we have final state and deserialized component already
                foreach (var (entity, componentsBatch) in batchStates)
                {
                    // we have to create the entity if it doesn't exist directly
                    // not via CommandBuffer as we need to map it as there will be no other moment to do it
                    // so at first we have to create it with an empty archetype and then add components to it in a batch by CommandBuffer
                    // it is sub-optimal but I don't see the way to do it better

                    if (!entitiesMap.TryGetValue(entity, out var realEntity))
                        entitiesMap[entity] = realEntity = world.Create();

                    foreach (var batchState in componentsBatch.Values)
                    {
                        if (batchState.reconciliationState.Last == CRDTReconciliationEffect.NoChanges)
                            continue;

                        batchState.sdkComponentBridge.CommandBufferSynchronizer.Apply(world, commandBuffer, realEntity,
                            batchState.reconciliationState.Last, batchState.deserializationTarget);
                    }
                }
            }
            finally
            {
                finalized = true;

                // it is a must to Playback to clear internals
                commandBuffer.Playback();
                Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var componentsBatch in batchStates.Values)
                componentsBatch.Dispose();

            batchStates.Dispose();
            deletedEntities.Dispose();
        }
    }
}
