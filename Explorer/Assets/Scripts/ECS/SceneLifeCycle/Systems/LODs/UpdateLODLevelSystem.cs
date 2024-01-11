using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using NSubstitute.ReturnsExtensions;
using Realm;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByRadiusSystem))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class UpdateLODLevelSystem : BaseUnityLoopSystem
    {
        private readonly Vector2Int[] bucketLodsLimits;

        public UpdateLODLevelSystem(World world, Vector2Int[] bucketLodLimits) : base(world)
        {
            bucketLodsLimits = bucketLodLimits;
        }

        protected override void Update(float t)
        {
            CleanLODVisualStateQuery(World);
            UpdateLODLevelQuery(World);
        }

        //TODO: HORRIBLE, FIX!!
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CleanLODVisualState(ref VisualSceneState visualSceneState)
        {
            if (visualSceneState is { IsDirty: true, CurrentVisualSceneState: VisualSceneStateEnum.SHOWING_LOD })
            {
                visualSceneState.IsDirty = false;
            }
        }
        

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref VisualSceneState visualSceneState, ref SceneLOD sceneLOD,
            ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.IsEmpty) return; //We dont update LODs of empty scenes
            if (!visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD)) return;

            if (ShouldSwapLOD(partition.Bucket, sceneLOD.currentLODLevel))
                sceneLOD.UpdateLOD(partition.Bucket);
        }

        private bool ShouldSwapLOD(byte partitionBucket, int currentLODLevel)
        {
            if (partitionBucket > bucketLodsLimits[0][0] && partitionBucket <= bucketLodsLimits[0][1] &&
                currentLODLevel == 2) return false;
            if (partitionBucket > bucketLodsLimits[1][0] && currentLODLevel == 3) return false;
            return true;
        }
    }
}