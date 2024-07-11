using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using DCL.Diagnostics;
using ECS.LifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UnloadSceneLODSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IScenesCache scenesCache;

        public UnloadSceneLODSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UnloadLODQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortSucceededLODPromisesQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadLOD(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, World);
            World.Remove<SceneLODInfo, VisualSceneState, DeleteEntityIntention>(entity);
        }

        [Query]
        private void AbortSucceededLODPromises(ref SceneLODInfo sceneLODInfo)
        {
            foreach (var lodAsset in sceneLODInfo.LODAssets)
            {
                if (!lodAsset.LODPromise.IsConsumed && lodAsset.LODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result) && result.Succeeded)
                    result.Asset!.Dispose();
                else
                    lodAsset.LODPromise.ForgetLoading(World);
            }
        }
    }
}
