using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiling;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.SceneBoundsChecker;
using SceneRunner.Scene;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;


namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly ILODAssetsPool lodCache;
        private readonly ILODSettingsAsset lodSettingsAsset; 
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        internal IPerformanceBudget frameCapBudget;


        private readonly Transform lodsTransformParent;
        private readonly TextureArrayContainer lodTextureArrayContainer;


        public UpdateSceneLODInfoSystem(World world, ILODAssetsPool lodCache, ILODSettingsAsset lodSettingsAsset,
            IPerformanceBudget memoryBudget, IPerformanceBudget frameCapBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue,
            Transform lodsTransformParent, TextureArrayContainer lodTextureArrayContainer) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodsTransformParent = lodsTransformParent;
            this.lodTextureArrayContainer = lodTextureArrayContainer;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODPromiseQuery(World);
            InstantiateCurrentLODQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if ((partitionComponent.IsDirty || sceneLODInfo.CurrentLODLevel == byte.MaxValue) && !partitionComponent.IsBehind)
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
        }


        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void InstantiateCurrentLOD(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty || sceneLODInfo.CurrentLOD == null) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            var currentLOD = sceneLODInfo.CurrentLOD;

            if (currentLOD.State == LODAsset.LOD_STATE.WAITING_INSTANTIATION &&
                currentLOD.AsyncInstantiation.IsWaitingForSceneActivation())
            {
                FinalizeInstantiation(currentLOD, sceneDefinitionComponent);
                sceneLODInfo.UpdateCurrentVisibleLOD();
                if (currentLOD!.LodKey.Level == 0)
                {
                    scenesCache.Add(sceneLODInfo, sceneDefinitionComponent.Parcels);
                    CheckSceneReadiness(sceneDefinitionComponent);
                }
                sceneLODInfo.IsDirty = false;
            }
        }



        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty || sceneLODInfo.CurrentLODPromise.IsConsumed) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                LODAsset newLod = default;
                if (result.Succeeded)
                {
                    //NOTE: Using the count API since the one without count does not parent correctly.

                    var asyncInstantiation =
                        Object.InstantiateAsync(result.Asset!.GetMainAsset<GameObject>(), 1,
                            lodsTransformParent, sceneDefinitionComponent.SceneGeometry.BaseParcelPosition, Quaternion.identity);
                    asyncInstantiation.allowSceneActivation = false;
                    
                    newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel),
                        lodCache, result.Asset, asyncInstantiation);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(),
                        $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");
                    newLod = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel), lodCache);
                    if (newLod.LodKey.Level == 0)
                    {
                        scenesCache.Add(sceneLODInfo, sceneDefinitionComponent.Parcels);
                        CheckSceneReadiness(sceneDefinitionComponent);
                    }
                    sceneLODInfo.IsDirty = false;
                }
                sceneLODInfo.SetCurrentLOD(newLod);
            }
        }

        private void CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            if (sceneLODCandidate != sceneLODInfo.CurrentLODLevel)
                UpdateLODLevel(ref partitionComponent, ref sceneLODInfo, sceneLODCandidate, sceneDefinitionComponent);
        }

        private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
            byte sceneLODCandidate, SceneDefinitionComponent sceneDefinitionComponent)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);
            
            //TODO (Juani) : Remove this hardcoded number once we have only two lod levels
            sceneLODInfo.CurrentLODLevel = (byte)(sceneLODCandidate > 0 ? 3 : 0);
            var newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel);

            //If the current LOD is the candidate, no need to make a new promise or set anything new
            if (newLODKey.Equals(sceneLODInfo.CurrentLOD))
            {
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (newLODKey.Equals(sceneLODInfo.CurrentVisibleLOD))
            {
                sceneLODInfo.ResetToCurrentVisibleLOD();
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                sceneLODInfo.SetCurrentLOD(cachedAsset);
                sceneLODInfo.IsDirty = false;
                if (sceneLODInfo.CurrentLOD?.LodKey.Level == 0)
                    CheckSceneReadiness(sceneDefinitionComponent);
                return;
            }

            string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
            var manifest = LODUtils.LOD_MANIFESTS[newLODKey.Level];

            var assetBundleIntention =  GetAssetBundleIntention.FromHash(typeof(GameObject),
                platformLODKey,
                permittedSources: lodSettingsAsset.EnableLODStreaming ? AssetSource.ALL : AssetSource.EMBEDDED,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                manifest: manifest);

            sceneLODInfo.CurrentLODPromise =
                Promise.Create(World, assetBundleIntention, partitionComponent);

            sceneLODInfo.IsDirty = true;
        }

        private void FinalizeInstantiation(LODAsset currentLOD, SceneDefinitionComponent sceneDefinitionComponent)
        {
            currentLOD.AsyncInstantiation.allowSceneActivation = true;
            currentLOD.AsyncInstantiation.WaitForCompletion();

            var newRoot = currentLOD.AsyncInstantiation.Result[0];
            var slots = Array.Empty<TextureArraySlot?>();
            if (!currentLOD.LodKey.Level.Equals(0))
            {
                slots = LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent.Definition.id,
                    sceneDefinitionComponent.Definition.metadata.scene.DecodedBase, newRoot, lodTextureArrayContainer);
            }

            currentLOD?.FinalizeInstantiation(newRoot, slots);
        }

        private void CheckSceneReadiness(SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneReadinessReportQueue.TryDequeue(sceneDefinitionComponent.Parcels, out var reports))
            {
                for (int i = 0; i < reports!.Value.Count; i++)
                {
                    var report = reports.Value[i];
                    report.ProgressCounter.Value = 1f;
                    report.CompletionSource.TrySetResult();
                }
            }
        }
    }
}
