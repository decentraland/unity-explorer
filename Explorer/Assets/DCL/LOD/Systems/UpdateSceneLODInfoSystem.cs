using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AssetsProvision;
using DCL.AsyncLoadReporting;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
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
using UnityEngine.Experimental.Rendering;
using Utility;
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
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private readonly Transform lodsTransformParent;

        private readonly IExtendedObjectPool<Material> materialPool;
        private readonly Dictionary<TextureFormat, TextureArrayContainer_ToDelete> textureArrayContainerDictionary;

        public UpdateSceneLODInfoSystem(World world, ILODAssetsPool lodCache, ILODSettingsAsset lodSettingsAsset,
            IPerformanceBudget memoryBudget, IPerformanceBudget frameCapBudget, IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, Transform lodsTransformParent, IExtendedObjectPool<Material> materialPool) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.lodsTransformParent = lodsTransformParent;
            this.materialPool = materialPool;
            textureArrayContainerDictionary = new Dictionary<TextureFormat, TextureArrayContainer_ToDelete>
            {
                {
                    TextureFormat.BC7, new TextureArrayContainer_ToDelete(TextureFormat.BC7)
                },
                {
                    TextureFormat.DXT1, new TextureArrayContainer_ToDelete(TextureFormat.DXT1)
                },
                {
                    TextureFormat.DXT5, new TextureArrayContainer_ToDelete(TextureFormat.DXT5)
                }
            };
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODPromiseQuery(World);
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
        private void ResolveCurrentLODPromise(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneLODInfo.IsDirty) return;

            if (!(frameCapBudget.TrySpendBudget() && memoryBudget.TrySpendBudget())) return;

            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                sceneLODInfo.CurrentLOD?.Release();

                if (result.Succeeded)
                {
                    GameObject? instantiatedLOD = Object.Instantiate(result.Asset.GetMainAsset<GameObject>(), sceneDefinitionComponent.SceneGeometry.BaseParcelPosition,
                        Quaternion.identity, lodsTransformParent);
                    if (!sceneLODInfo.CurrentLODLevel.Equals(0))
                    {
                        sceneLODInfo.CurrentLOD = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel),
                            instantiatedLOD, result.Asset, lodCache, LODUtils.ApplyTextureArrayToLOD(sceneDefinitionComponent, instantiatedLOD, materialPool, textureArrayContainerDictionary, sceneLODInfo.CurrentLODLevel), materialPool);
                    }
                    else
                    {
                        sceneLODInfo.CurrentLOD = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel),
                            instantiatedLOD, result.Asset, lodCache);
                    }

                    ConfigureSceneMaterial.EnableSceneBounds(in instantiatedLOD,
                        in sceneDefinitionComponent.SceneGeometry.CircumscribedPlanes);
                }
                else
                {
                    ReportHub.LogWarning(GetReportCategory(),
                        $"LOD request for {sceneLODInfo.CurrentLODPromise.LoadingIntention.Hash} failed");

                    sceneLODInfo.CurrentLOD = new LODAsset(new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel));
                }
                scenesCache.Add(sceneLODInfo, sceneDefinitionComponent.Parcels);
                CheckSceneReadiness(sceneDefinitionComponent);
                sceneLODInfo.IsDirty = false;
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
            sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
            var newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, sceneLODInfo.CurrentLODLevel);

            //If the current LOD is the candidate, no need to make a new promise or set anything new
            if (newLODKey.Equals(sceneLODInfo.CurrentLOD))
            {
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                //If its cached, no need to make a new promise
                sceneLODInfo.CurrentLOD?.Release();
                sceneLODInfo.CurrentLOD = cachedAsset;
                sceneLODInfo.IsDirty = false;
                CheckSceneReadiness(sceneDefinitionComponent);
            }
            else
            {
                var assetBundleIntention =  GetAssetBundleIntention.FromHash(newLODKey + PlatformUtils.GetPlatform(),
                    permittedSources: AssetSource.EMBEDDED,
                    customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES[newLODKey.Level]);
                //TODO: (ASK MISHA) Is there some way to avoid this allocation?
                assetBundleIntention.Manifest =  new SceneAssetBundleManifest(LODUtils.LOD_WEB_URL,
                    new SceneAbDto
                    {
                        files = new[]
                        {
                            newLODKey + PlatformUtils.GetPlatform()
                        },
                    });
                
                sceneLODInfo.CurrentLODPromise =
                    Promise.Create(World, assetBundleIntention, partitionComponent);

                sceneLODInfo.IsDirty = true;
            }
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