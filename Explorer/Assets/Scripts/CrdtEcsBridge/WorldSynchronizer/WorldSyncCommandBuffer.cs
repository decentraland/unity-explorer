using Arch.CommandBuffer;
using Arch.Core;
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
    public class WorldSyncCommandBuffer : IWorldSyncCommandBuffer
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
                { (CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentAdded), CRDTReconciliationEffect.ComponentModified },
                { (CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified), CRDTReconciliationEffect.ComponentModified },

                // if it was deleted then delete from World
                { (CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentDeleted), CRDTReconciliationEffect.ComponentDeleted },

                // if component already existed then if the component still exists it should be modified
                { (CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentModified), CRDTReconciliationEffect.ComponentModified },
                { (CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentAdded), CRDTReconciliationEffect.ComponentModified },

                // if it was deleted then delete from World
                { (CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentDeleted), CRDTReconciliationEffect.ComponentDeleted },
            };

        private readonly Dictionary<CRDTEntity, Dictionary<int, BatchState>> batchStates;
        private readonly List<CRDTEntity> deletedEntities;

        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly IEntityFactory entityFactory;
        private readonly WorldSyncCommandBufferCollectionsPool collectionsPool;

        private bool finalized;
        private bool deserialized;

        /// <summary>
        /// Can't contain a public ctor as should be instantiated within the assembly
        /// </summary>
        internal WorldSyncCommandBuffer(ISDKComponentsRegistry componentsRegistry, IEntityFactory entityFactory, WorldSyncCommandBufferCollectionsPool collectionsPool)
        {
            batchStates = collectionsPool.GetMainDictionary();
            deletedEntities = collectionsPool.GetDeletedEntities();

            this.sdkComponentsRegistry = componentsRegistry;
            this.entityFactory = entityFactory;
            this.collectionsPool = collectionsPool;
        }

        /// <summary>
        /// For tests only
        /// </summary>
        internal CRDTReconciliationEffect GetLastState(CRDTEntity entity, int componentId) =>
            batchStates.TryGetValue(entity, out var componentsBatch) &&
            componentsBatch.TryGetValue(componentId, out var batchState)
                ? batchState.reconciliationState.Last
                : CRDTReconciliationEffect.NoChanges;

        /// <summary>
        /// Add messages in the order they are processed by CRDT
        /// This sync function is for ECS syncing and it heavily relies on the proper result from the CRDT Protocol
        /// </summary>
        /// <returns>The last status corresponding to the (<see cref="CRDTMessage.EntityId"/>, <see cref="CRDTMessage.ComponentId"/>)</returns>
        public CRDTReconciliationEffect SyncCRDTMessage(in CRDTMessage message, CRDTReconciliationEffect reconciliationEffect)
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
                        ReleaseBatchStateDictionary(componentsBatch);
                        batchStates.Remove(message.EntityId);
                    }

                    return CRDTReconciliationEffect.EntityDeleted;
                case CRDTReconciliationEffect.ComponentAdded:
                case CRDTReconciliationEffect.ComponentDeleted:
                case CRDTReconciliationEffect.ComponentModified:

                    if (!sdkComponentsRegistry.TryGet(message.ComponentId, out var sdkComponentBridge))
                    {
                        Debug.LogWarning($"SDK Component {message.ComponentId} is not registered");
                        return CRDTReconciliationEffect.NoChanges;
                    }

                    // Store the first and the last result
                    bool componentBatchExists;

                    if ((componentBatchExists = batchStates.TryGetValue(message.EntityId, out componentsBatch))
                        && componentsBatch.TryGetValue(message.ComponentId, out var state))
                    {
                        // take the first one, override the last one
                        state.reconciliationState = new ReconciliationState(state.reconciliationState.First, reconciliationEffect);

                        // The data must be taken from the last message
                        state.crdtMessage = message;
                        return reconciliationEffect;
                    }

                    if (!componentBatchExists)
                        componentsBatch = batchStates[message.EntityId] = collectionsPool.GetInnerDictionary();

                    // the first and the last are the same
                    // take the component from the pool
                    var batchState = BatchState.POOL.Get();
                    batchState.crdtMessage = message;
                    batchState.reconciliationState = new (reconciliationEffect, reconciliationEffect);
                    batchState.sdkComponentBridge = sdkComponentBridge;
                    componentsBatch[message.ComponentId] = batchState;
                    return reconciliationEffect;
            }

            return reconciliationEffect;
        }

        /// <summary>
        /// Resolves the final state of (entity, component) <br/>
        /// Deserializes only the last state of the component <br/>
        /// Should be called from the background thread <br/>
        /// </summary>
        public void FinalizeAndDeserialize()
        {
            // resolve the final state

            foreach (Dictionary<int, BatchState> componentsBatch in batchStates.Values)
            {
                // It may contain extra elements (that will be resolved as NoChanges, it is not a problem
                // as we use `ArrayPool` anyway

                // Indicator if there were any changes after the full reconciliation
                var containsAnyChanges = false;

                foreach (var batchState in componentsBatch.Values)
                {
                    var finalState = MERGE_MATRIX[batchState.reconciliationState];

                    // just override with the final state
                    batchState.reconciliationState = new ReconciliationState(finalState, finalState);

                    if (finalState != CRDTReconciliationEffect.ComponentDeleted)
                    {
                        var bridge = batchState.sdkComponentBridge;
                        var deserializationTarget = batchState.sdkComponentBridge.Pool.Rent();
                        bridge.Serializer.DeserializeInto(deserializationTarget, batchState.crdtMessage.Data.Memory.Span);
                        batchState.deserializationTarget = deserializationTarget;
                    }

                    containsAnyChanges |= finalState != CRDTReconciliationEffect.NoChanges;
                }

                // If there were no changes clean up the batch
                // it will be returned to the pool on Apply or Dispose so it prevents the modification of the collection while it's being iterated over
                if (!containsAnyChanges)
                {
                    foreach (BatchState batchState in componentsBatch.Values)
                        BatchState.POOL.Release(batchState);

                    componentsBatch.Clear();
                }
            }

            deserialized = true;
        }

        internal void Apply(World world, PersistentCommandBuffer commandBuffer, Dictionary<CRDTEntity, Entity> entitiesMap)
        {
            if (!deserialized)
                throw new InvalidOperationException($"{nameof(FinalizeAndDeserialize)} must be called before {nameof(Apply)}");

            // TODO consider adding something like "isDirty" to the component (via State or Tag?), resolve it with the first implemented systems
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

                    if (componentsBatch.Count == 0) continue;

                    if (!entitiesMap.TryGetValue(entity, out var realEntity))
                        entitiesMap[entity] = realEntity = entityFactory.Create(entity, world);

                    foreach (var batchState in componentsBatch.Values)
                    {
                        if (batchState.reconciliationState.Last == CRDTReconciliationEffect.NoChanges)
                            continue;

                        // TODO add proper logging with Levels
                        // Debug.Log($"{entity} -> {realEntity}: Apply {batchState.sdkComponentBridge.ComponentType.Name} {batchState.reconciliationState.Last}");

                        batchState.sdkComponentBridge.CommandBufferSynchronizer.Apply(world, commandBuffer, realEntity,
                            batchState.reconciliationState.Last, batchState.deserializationTarget);
                    }
                }
            }
            finally
            {
                // it is a must to Playback to clear internals
                commandBuffer.Playback();
                Dispose();
            }
        }

        /// <summary>
        /// <inheritdoc cref="IWorldSyncCommandBuffer.Apply"/>
        /// </summary>
        void IWorldSyncCommandBuffer.Apply(World world, PersistentCommandBuffer commandBuffer, Dictionary<CRDTEntity, Entity> entitiesMap)
        {
            Apply(world, commandBuffer, entitiesMap);
        }

        private void ReleaseBatchStateDictionary(Dictionary<int, BatchState> inner)
        {
            foreach (BatchState batchState in inner.Values)
                BatchState.POOL.Release(batchState);

            collectionsPool.ReleaseInnerDictionary(inner);
        }

        public void Dispose()
        {
            if (finalized) return;

            foreach (Dictionary<int, BatchState> componentsBatch in batchStates.Values) ReleaseBatchStateDictionary(componentsBatch);

            collectionsPool.ReleaseMainDictionary(batchStates);
            collectionsPool.ReleaseDeletedEntities(deletedEntities);

            finalized = true;
        }
    }
}
