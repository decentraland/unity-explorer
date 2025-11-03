using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using DCL.Diagnostics;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UnloadSceneLODSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ILODCache lodCache;

        public UnloadSceneLODSystem(World world, IScenesCache scenesCache, ILODCache lodCache) : base(world)
        {
            this.scenesCache = scenesCache;
            this.lodCache = lodCache;
        }

        protected override void Update(float t)
        {
            MoveAssetsToBridgeQuery(World);
            UnloadLODQuery(World);
            UnloadLODWhenSceneReadyQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortSucceededLODPromisesQuery(World);
            DestroySceneLODQuery(World);
        }


        //TODO (JUANI): Think of cancellation of scene loading
        [Query]
        [All(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void MoveAssetsToBridge(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.InitialSceneStateLOD.CurrentState != InitialSceneStateLOD.InitialSceneStateLODState.RESOLVED)
                return;

            if (sceneLODInfo.InitialSceneStateLOD.AssetsInTheBridge)
                return;

            if (sceneLODInfo.IsInitialized())
                sceneLODInfo.metadata.SuccessfullLODs = SceneLODInfoUtils.ClearLODResult(sceneLODInfo.metadata.SuccessfullLODs, 0);

            if (!partitionComponent.IsBehind)
                sceneLODInfo.InitialSceneStateLOD.MoveAssetsToBridge();

            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
            sceneLODInfo.InitialSceneStateLOD.AssetsInTheBridge = true;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        [None(typeof(ISceneFacade))]
        private void UnloadLOD(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
            World.Remove<SceneLODInfo, DeleteEntityIntention>(entity);
        }

        [Query]
        private void UnloadLODWhenSceneReady(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref SceneLODInfo sceneLODInfo, ref ISceneFacade sceneFacade, ref SceneLoadingState sceneLoadingState)
        {
            if (sceneLoadingState.VisualSceneState == VisualSceneState.SHOWING_SCENE)
            {
                //TODO(Juani) : This `if` is required for retro-compatibility with non Initial Scene State scenes.
                //If all scenes were built with SAB scenes, we could remove it
                if (sceneLODInfo.InitialSceneStateLOD.CurrentState == InitialSceneStateLOD.InitialSceneStateLODState.RESOLVED)
                {
                    World.Remove<SceneLODInfo>(entity);
                    return;
                }

                if (!sceneFacade.IsSceneReady())
                    return;

                sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
                World.Remove<SceneLODInfo>(entity);
            }
        }

        //TODO (JUANI): ANALYZE
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
            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
        }
    }
}
