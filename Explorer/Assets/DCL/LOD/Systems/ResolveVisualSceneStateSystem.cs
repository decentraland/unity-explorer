using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    [UpdateBefore(typeof(UpdateVisualSceneStateSystem))]
    [UpdateBefore(typeof(UpdateSceneLODInfoSystem))]
    public partial class ResolveVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly ProvidedAsset<LODSettingsAsset> lodSettingsAsset;

        public ResolveVisualSceneStateSystem(World world, ProvidedAsset<LODSettingsAsset> lodSettingsAsset) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
        }

        protected override void Update(float t)
        {
            UpdateVisualStateQuery(World);
            AddSceneVisualStateQuery(World);
            UpdateVisualStateQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VisualSceneState))]
        private void AddSceneVisualState(in Entity entity, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            VisualSceneState visualSceneState = new VisualSceneState();
            ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent);
            visualSceneState.IsDirty = false;
            World.Add(entity, visualSceneState);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [Any(typeof(SceneLODInfo), typeof(SceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void UpdateVisualState(ref VisualSceneState visualSceneState, ref PartitionComponent partition,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (partition.IsDirty)
                ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent);
        }

        private void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition,
            SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If the scene is empty, no lods are possible
            if (sceneDefinitionComponent.IsEmpty)
            {
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
            }
            //For SDK6 scenes, we just show lod0
            else if (!sceneDefinitionComponent.IsSDK7)
            {
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
            }
            else
            {
                VisualSceneStateEnum candidateState = partition.Bucket < lodSettingsAsset.Value.LodPartitionBucketThresholds[0]
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;
                if (candidateState != visualSceneState.CurrentVisualSceneState)
                {
                    visualSceneState.CurrentVisualSceneState = candidateState;
                    visualSceneState.IsDirty = true;
                }
            }

        }

    }
}
