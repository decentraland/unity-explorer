using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
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
        private readonly IRealmData realmData;

        public UpdateLODLevelSystem(World world, Vector2Int[] bucketLodLimits) : base(world)
        {
            bucketLodsLimits = bucketLodLimits;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(ISceneFacade))]
        private void SwapVisualStateToScene(in Entity entity, ref VisualSceneState visualSceneState, 
            ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partition)
        {
            if (!visualSceneState.IsDirty) return;
            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                //Clear the currentLOD
                //sceneLODInfo.currentLOD
                
                //Show Scene
                World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                        partition));
                
                World.Remove<SceneLODInfo>(entity);
            }
            visualSceneState.IsDirty = false;
        }
        
        [Query]
        [None(typeof(ISceneFacade), typeof(DeleteEntityIntention))]
        private void AbortLoadingScenes(in Entity entity, ref VisualSceneState visualSceneState, 
            ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            if (!visualSceneState.IsDirty) return;
            
            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                promise.ForgetLoading(World);
                World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>, DeleteEntityIntention>(entity);
                
                //Show the currentLOD
                //sceneLODInfo
                
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