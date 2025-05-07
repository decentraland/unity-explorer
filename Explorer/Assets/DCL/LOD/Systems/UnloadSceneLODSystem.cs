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
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
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
            UnloadLODQuery(World);
            UnloadLODWhenSceneReadyQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortSucceededLODPromisesQuery(World);
            DestroySceneLODQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        [None(typeof(ISceneFacade))]
        private void UnloadLOD(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
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

                sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
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
            sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
        }
    }
}
