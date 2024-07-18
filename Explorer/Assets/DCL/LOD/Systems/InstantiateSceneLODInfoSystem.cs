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
        internal IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly ILODAssetsPool lodCache;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly TextureArrayContainer lodTextureArrayContainer;
        private readonly Transform lodsTransformParent;
        private GameObjectPool<LODGroup> lodGroupPool;

        public InstantiateSceneLODInfoSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, GameObjectPool<LODGroup> lodGroupPool, ILODAssetsPool lodCache, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, TextureArrayContainer lodTextureArrayContainer, Transform lodsTransformParent) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.lodCache = lodCache;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
            this.lodsTransformParent = lodsTransformParent;
            this.lodGroupPool = lodGroupPool;
        }


        protected override void Update(float t)
        {
            ResolveCurrentLODPromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.ArePromisesConsumed()) // Only continue if promises need to be processed
                return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) // Don't process promises if budget is maxxed out
                return;

            bool bNewAssetAdded = false; // Used to know whether the LODGroup need re-evaluating

            foreach (var lodAsset in sceneLODInfo.LODAssets)
            {
                if (lodAsset.LODPromise.IsConsumed)
                    continue;

                if (lodAsset.LODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
                {
                    if (result.Succeeded)
                    {
                        Transform lodGroupTransform = sceneLODInfo.CreateLODGroup(lodGroupPool, lodsTransformParent);

                        GameObject instantiatedLOD = Object.Instantiate(result.Asset!.GetMainAsset<GameObject>(),
                                                                            sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                                                                            Quaternion.identity,
                                                                            lodGroupTransform);

                        var slots = Array.Empty<TextureArraySlot?>();
                        if (!lodAsset.LodKey.Level.Equals(0))
                        {
                            slots = LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent.Definition.id,
                                                                    sceneDefinitionComponent.Definition.metadata.scene.DecodedBase,
                                                                    instantiatedLOD,
                                                                    lodTextureArrayContainer);
                        }

                        lodAsset.SetAssetBundleReference(result.Asset);
                        lodAsset.FinalizeInstantiation(instantiatedLOD, slots);
                        bNewAssetAdded = true;
                    }
                    else
                    {
                        ReportHub.LogWarning(GetReportCategory(),$"LOD request for {lodAsset.LODPromise.LoadingIntention.Hash} failed");
                        lodAsset.State = LODAsset.LOD_STATE.FAILED;
                    }

                    CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
                }
            }
            if(bNewAssetAdded)
                sceneLODInfo.ReEvaluateLODGroup();
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
            LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
        }
    }
}
