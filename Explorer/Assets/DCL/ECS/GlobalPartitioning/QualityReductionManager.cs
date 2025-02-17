using Arch.Core;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using System;

namespace DCL.ECS.GlobalPartitioning
{
    public enum MemoryDomain
    {
        AVATARS = 0, // Avatars are more important than scenes
        SCENES = 1,
    }

    /// <summary>
    ///     Component to identify entities within the same batch
    /// </summary>
    public readonly struct Batch<T>
    {
        public readonly Entity BatchEntity;

        public readonly T[] Entries;

        public readonly IPartitionComponent PartitionComponent;

        public readonly MemoryDomain Domain;

        public Batch(Entity batchEntity, T[] entries, IPartitionComponent partitionComponent, MemoryDomain domain)
        {
            Entries = entries;
            PartitionComponent = partitionComponent;
            Domain = domain;
            BatchEntity = batchEntity;
        }
    }

    /// <summary>
    ///     Identifies operation on the batch is in progress,
    ///     thus all other batches must wait for it
    /// </summary>
    public readonly struct BatchInProgress { }

    /// <summary>
    ///     It's just the data flow, it's not how it should be designed
    /// </summary>
    public readonly struct SceneInfo
    {
        public readonly Entity Entity;
        public readonly SceneDefinitionComponent SceneDefinitionComponent;

        public SceneInfo(Entity entity, SceneDefinitionComponent sceneDefinitionComponent)
        {
            Entity = entity;
            SceneDefinitionComponent = sceneDefinitionComponent;
        }
    }

    /// <summary>
    ///     Knows about every batch and priorities between them
    /// </summary>
    public class QualityReductionManager
    {
        /// <summary>
        ///     Called when a new batch is created, or the priority of the batch has changed.
        ///     1. First it must be called for existing batches to update their priority and possibly to unload them if required
        ///     2. Second it must be called for new batches
        /// </summary>
        /// <returns>Process has started</returns>
        public bool ResolveBatch<T>(World world, Batch<T> batch)
        {
            // 1. First get all possible tiers for the current batch
            IQualityReductionStrategy<T> strategy = GetStrategyForBatch<T>();

            BatchTier renewedBatchTier = strategy.CreateBatchTierConfiguration(in batch);
            ref BatchTier previousBatchTier = ref world.TryGetRef<BatchTier>(batch.BatchEntity, out bool previousBatchTierExists);

            BatchTier? previousBatchTierNullable = previousBatchTierExists ? previousBatchTier : null;

            // Properly compare both tiers
            if (renewedBatchTier.Equals(previousBatchTier))
                return false; // Nothing to do - priorities are not changed

            // if the desired result is empty, it means that the batch must be culled
            if (renewedBatchTier.currentTierIndex == BatchTier.CULLED)
            {
                // If previously it was not culled Unload it now
                if (previousBatchTierExists) { strategy.ChangeBatchTier(world, in batch, previousBatchTierNullable, in renewedBatchTier, (byte)renewedBatchTier.currentTierIndex); }
            }

            int availableMemory = GetAvailableMemory();

            // Starting from the highest tier (it's the desired one) and going down
            // apply one that requires minimal unloading (this strategy can be configurable, flexible)

            for (var tier = (sbyte)(renewedBatchTier.possibleTiers.Length - 1); tier >= 0; tier--)
            {
                int tierWeight = renewedBatchTier.possibleTiers[tier].Weight;

                // Adjust tier weight by the difference with the previously loaded tier
                if (previousBatchTierExists)
                    tierWeight -= previousBatchTier.currentTier.Weight;

                if (availableMemory >= tierWeight)
                {
                    // Happy path

                    world.Add(batch.BatchEntity, new BatchInProgress());
                    strategy.ChangeBatchTier(world, in batch, previousBatchTierNullable, in renewedBatchTier, (byte)tier);
                    ChangeReservedMemory(tierWeight);
                    return true;
                }
            }

            // Try to unload less priority batches
            return TryUnloadLessPriorityBatches(world, in batch);

            // As all unloading operation are asynchronous and/or can be throttled, we can't continue right after
        }

        private bool TryUnloadLessPriorityBatches<T>(World world, in Batch<T> currentBatch) =>

            // query other batches
            // Compare their Partition Component and Memory Domain with the current one
            // Try to unload as minimum as possible
            // Add BatchInProgress to every batch that is being unloaded
            // When the batch is unloaded Change Reserved Memory
            true; // Returns true is something can be unloaded

        private IQualityReductionStrategy<T> GetStrategyForBatch<T>() =>
            throw new NotImplementedException();

        /// <summary>
        ///     Decrease or increase reserved memory
        /// </summary>
        /// <param name="memory"></param>
        private void ChangeReservedMemory(int memory) { }

        private int GetAvailableMemory() =>

            // Ask memory budget for a value
            0;
    }

    public interface IQualityReductionStrategy<T>
    {
        /// <summary>
        ///     Rules:
        ///     1. If BatchTier is empty it means that the desired result is CULLED (OK Situation)
        ///     2. BatchTier can contain a single element - this is the only result desired
        ///     3. Multiple elements are sorted from least to most memory consumption
        ///     4. The last element is the most desired one
        /// </summary>
        /// <param name="batch"></param>
        /// <returns></returns>
        BatchTier CreateBatchTierConfiguration(in Batch<T> batch);

        void ChangeBatchTier(World world, in Batch<T> batch, in BatchTier? from, in BatchTier to, byte targetTier);
    }
}
