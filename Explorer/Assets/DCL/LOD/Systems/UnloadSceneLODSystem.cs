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
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UnloadSceneLODSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly ILODCache lodCache;
        private readonly IGltfContainerAssetsCache assetsCache;


        public UnloadSceneLODSystem(World world, IScenesCache scenesCache, ILODCache lodCache, IGltfContainerAssetsCache assetsCache) : base(world)
        {
            this.scenesCache = scenesCache;
            this.lodCache = lodCache;
            this.assetsCache = assetsCache;
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
            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
            World.Remove<SceneLODInfo, DeleteEntityIntention>(entity);
        }

        [Query]
        private void UnloadLODWhenSceneReady(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref SceneLODInfo sceneLODInfo, ref ISceneFacade sceneFacade, ref SceneLoadingState sceneLoadingState, ref StaticSceneAssetBundle staticSceneAssetBundle)
        {
            if (sceneLoadingState.VisualSceneState == VisualSceneState.SHOWING_SCENE)
            {
                if (staticSceneAssetBundle.Supported && sceneLODInfo.HasLOD(0))
                {
                    for (var i = 0; i < staticSceneAssetBundle.StaticSceneDescriptor.assetHash.Count; i++)
                    {
                        string assetHash = staticSceneAssetBundle.StaticSceneDescriptor.assetHash[i];
                        assetsCache.Dereference(assetHash, sceneLODInfo.GltfContainerAssets[i]);
                    }

                    sceneLODInfo.metadata.SuccessfullLODs = SceneLODInfoUtils.ClearLODResult(sceneLODInfo.metadata.SuccessfullLODs, 0);
                    sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
                    World.Remove<SceneLODInfo>(entity);
                    return;
                }

                if (!sceneFacade.IsSceneReady())
                    return;

                sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
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
            sceneLODInfo.DisposeSceneLODAndReleaseToCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache, World);
        }
    }
}
