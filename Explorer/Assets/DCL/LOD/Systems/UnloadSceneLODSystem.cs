using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.LOD;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using DCL.Diagnostics;
using DCL.LOD.Systems;
using ECS.LifeCycle;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UnloadSceneLODSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ILODCache lodCache;

        private float defaultFOV;
        private float defaultLodBias;
        public IRealmPartitionSettings realmPartitionSettingsAsset;


        public UnloadSceneLODSystem(World world, IScenesCache scenesCache, ILODCache lodCache, IRealmPartitionSettings realmPartitionSettingsAsset) : base(world)
        {
            this.scenesCache = scenesCache;
            this.lodCache = lodCache;
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
        }

        public override void Initialize()
        {
            defaultFOV = World.CacheCamera().GetCameraComponent(World).Camera.fieldOfView;
            defaultLodBias = QualitySettings.lodBias;
        }

        protected override void Update(float t)
        {
            UnloadSceneLODForISSQuery(World);
            UnloadLODQuery(World);
            UnloadLODWhenSceneReadyQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortSucceededLODPromisesQuery(World);
            DestroySceneLODQuery(World);
        }


        [Query]
        [All(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void UnloadSceneLODForISS(in Entity entity, ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState sceneLoadingState)
        {
            if (sceneLoadingState.VisualSceneState == VisualSceneState.SHOWING_SCENE)
            {
                if (sceneLODInfo.InitialSceneStateLOD.CurrentState != InitialSceneStateLOD.InitialSceneStateLODState.RESOLVED)
                    return;

                sceneLODInfo.InitialSceneStateLOD.AssetsShouldGoToTheBridge = LODUtils.ShouldGoToTheBridge(partitionComponent);
                sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World,
                    defaultFOV, defaultLodBias,  realmPartitionSettingsAsset.MaxLoadingDistanceInParcels, sceneDefinitionComponent.Parcels.Count);
                World.Remove<SceneLODInfo>(entity);
            }

        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        [None(typeof(ISceneFacade))]
        private void UnloadLOD(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World,
                defaultFOV, defaultLodBias,  realmPartitionSettingsAsset.MaxLoadingDistanceInParcels, sceneDefinitionComponent.Parcels.Count);
            World.Remove<SceneLODInfo, DeleteEntityIntention>(entity);
        }

        [Query]
        private void UnloadLODWhenSceneReady(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref SceneLODInfo sceneLODInfo, ref ISceneFacade sceneFacade, ref SceneLoadingState sceneLoadingState)
        {
            if (sceneLoadingState.VisualSceneState == VisualSceneState.SHOWING_SCENE)
            {
                if (!sceneFacade.IsSceneReady())
                    return;

                sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World,
                    defaultFOV, defaultLodBias,  realmPartitionSettingsAsset.MaxLoadingDistanceInParcels, sceneDefinitionComponent.Parcels.Count);
                World.Remove<SceneLODInfo>(entity);
            }
        }

        [Query]
        private void AbortSucceededLODPromises(ref SceneLODInfo sceneLODInfo)
        {
            if (!sceneLODInfo.CurrentLODPromise.IsConsumed && sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result) && result.Succeeded)
                result.Asset!.Dispose();
            else
                sceneLODInfo.CurrentLODPromise.ForgetLoading(World);
        }

        [Query]
        private void DestroySceneLOD(ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World,
                defaultFOV, defaultLodBias,  realmPartitionSettingsAsset.MaxLoadingDistanceInParcels, sceneDefinitionComponent.Parcels.Count);
        }
    }
}
