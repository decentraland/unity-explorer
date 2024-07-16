using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
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


        public InstantiateSceneLODInfoSystem(World world, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, ILODAssetsPool lodCache, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, TextureArrayContainer lodTextureArrayContainer, Transform lodsTransformParent) : base(world)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.lodCache = lodCache;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
            this.lodsTransformParent = lodsTransformParent;
        }


        protected override void Update(float t)
        {
            ResolveCurrentLODPromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneLODInfo.ArePromisesConsumed())
                return;

            // if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget()))
            //     return;

            bool bNewAssetAdded = false;

            foreach (var lodAsset in sceneLODInfo.LODAssets)
            {
                if (lodAsset.LODPromise.IsConsumed)
                    continue;

                if (lodAsset.LODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
                {
                    if (result.Succeeded)
                    {
                        GameObject instantiatedLOD = Object.Instantiate(result.Asset!.GetMainAsset<GameObject>(),
                                                                            sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                                                                            Quaternion.identity,
                                                                            lodsTransformParent);

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
                sceneLODInfo.ReEvaluateLODGroup(lodsTransformParent);
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //if (IsLOD0(ref sceneLODInfo))
            {
                scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
                LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            }

            //sceneLODInfo.IsDirty = false;
        }

        private void FinalizeInstantiation(ref SceneLODInfo sceneLODInfo, LODAsset currentLOD, SceneDefinitionComponent sceneDefinitionComponent, GameObject instantiatedLOD, byte currentLODLevel)
        {


        }

        // private bool IsLOD0(ref SceneLODInfo sceneLODInfo)
        // {
        //     return sceneLODInfo.CurrentLOD.LodKey.Level == 0;
        // }
    }
}
