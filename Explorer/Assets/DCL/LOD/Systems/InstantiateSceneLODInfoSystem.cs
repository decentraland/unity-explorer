using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class InstantiateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly TextureArrayContainer lodTextureArrayContainer;
        internal IPerformanceBudget frameCapBudget;

        public InstantiateSceneLODInfoSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, TextureArrayContainer lodTextureArrayContainer) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
        }


        protected override void Update(float t)
        {
            ResolveCurrentLODPromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.CurrentLODPromise.IsConsumed) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) // Don't process promises if budget is maxxed out
                return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                LODAsset newLod = default;
                if (result.Succeeded)
                {
                    var instantiatedLOD = Object.Instantiate(result.Asset!.GetMainAsset<GameObject>(),
                        sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                        Quaternion.identity);
                    newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevelPromise), result.Asset);
                    FinalizeInstantiation(newLod, sceneDefinitionComponent, instantiatedLOD);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(), $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");
                    newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevelPromise));
                }

                sceneLODInfo.ReEvaluateLODGroup(newLod);
                CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
            }
        }

        private void FinalizeInstantiation(LODAsset currentLOD, SceneDefinitionComponent sceneDefinitionComponent, GameObject instantiatedLOD)
        {
            var slots = Array.Empty<TextureArraySlot?>();
            if (!currentLOD.LodKey.Level.Equals(0))
            {
                slots = LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent.Definition.id,
                    sceneDefinitionComponent.Definition.metadata.scene.DecodedBase, instantiatedLOD, lodTextureArrayContainer);
            }

            currentLOD.FinalizeInstantiation(instantiatedLOD, slots);
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
            LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
        }
    }
}