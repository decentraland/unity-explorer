using Arch.Core;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace DCL.ECS.GlobalPartitioning
{
    public class SceneQualityReductionStrategy : IQualityReductionStrategy<SceneInfo>
    {
        private const byte REAL_SCENE_TIER = 0;
        private const byte LOD_SCENE_TIER = 1;

        private readonly int SDK7Weight = 100;
        private readonly int SDK6Weight = 40;
        private readonly int LODWeight = 50;
        private readonly int RoadWeight = 25;

        private readonly byte LODBucketThreshold;

        private readonly HashSet<Vector2Int> roadCoordinates;

        public float GetUnloadingSqrRadius() =>
            throw new NotImplementedException();

        public BatchTier CreateBatchTierConfiguration(in Batch<SceneInfo> batch)
        {
            if (batch.PartitionComponent.RawSqrDistance > GetUnloadingSqrRadius())
                return new BatchTier(new FixedList32Bytes<BatchTier.TierCalculation>()); // The desired tier is CULLED

            var memoryRequiredForRealScenes = 0;
            var memoryRequiredForLODs = 0;

            foreach (SceneInfo info in batch.Entries)
            {
                int parcelsCount = info.SceneDefinitionComponent.Parcels.Count;

                if (roadCoordinates.Contains(info.SceneDefinitionComponent.Definition.metadata.scene.DecodedBase))
                {
                    memoryRequiredForRealScenes += RoadWeight;
                    memoryRequiredForLODs += RoadWeight;
                }
                else
                {
                    int singleParcelWeight = info.SceneDefinitionComponent.IsSDK7 ? SDK7Weight : SDK6Weight;

                    memoryRequiredForRealScenes += parcelsCount * singleParcelWeight;
                    memoryRequiredForLODs += parcelsCount * LODWeight;
                }
            }

            var possibleTears = new FixedList32Bytes<BatchTier.TierCalculation>();

            // Add LOD tier first
            possibleTears.Add(new BatchTier.TierCalculation(memoryRequiredForLODs));

            // If the real scene is needed according to the radius add it
            if (LODBucketThreshold > batch.PartitionComponent.Bucket)
                possibleTears.Add(new BatchTier.TierCalculation(memoryRequiredForRealScenes));

            return new BatchTier(possibleTears);
        }

        public void ChangeBatchTier(World world, in Batch<SceneInfo> batch, in BatchTier? from, in BatchTier to, byte targetTier)
        {
            // Based on the current and the target state change the visual state and all the required components for every scene

            if (to.Culled)
            {
                // Add delete components to all the scenes in the batch to unload LODs or real scenes

                // If this tier was loaded but now is no longer needed we can decrease the loading radius
                // It should be a gradual smooth process
                if (to.DesiredTier != targetTier)
                    DecrementRadiusByOneStep(batch.PartitionComponent);

                return;
            }

            // Call VisualSceneStateResolver, it will resolve the transition between different states

            // If this batch is successful we can increase the loading radius to let other batches come in
            if (to.DesiredTier == targetTier)
                IncreaseRadius();
        }

        private void IncreaseRadius()
        {
            // Keep loading and unloading radius in sync
        }

        private void DecrementRadiusByOneStep(IPartitionComponent partitionComponentToStepFrom)
        {
            // Calculate the desired radius from the given partition

            // Set the current radius accordingly

            // Keep loading and unloading radius in sync
        }
    }
}
