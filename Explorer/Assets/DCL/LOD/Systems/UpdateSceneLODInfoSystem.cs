using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
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
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;


        public UpdateSceneLODInfoSystem(World world, ILODAssetsPool lodCache, ILODSettingsAsset lodSettingsAsset,
            IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!partitionComponent.IsBehind)
            {
                byte lodForAcquisition = CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
                LODKey newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, lodForAcquisition);

                if (sceneLODInfo.LODAssets.Count == 0)
                {
                    AddLODAsset(ref sceneLODInfo, ref partitionComponent, newLODKey, lodForAcquisition);
                }
                else
                {
                    //foreach (var lodAsset in sceneLODInfo.LODAssets)
                    {
                        if (!sceneLODInfo.HasLODKey(newLODKey)) //If the current LOD is the candidate, no need to make a new promise or set anything new
                        {
                            AddLODAsset(ref sceneLODInfo, ref partitionComponent, newLODKey, lodForAcquisition);
                        }
                    }
                }
            }
        }

        private void AddLODAsset(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, LODKey newLODKey, byte lodForAcquisition)
        {
            LODAsset lodAsset = new LODAsset(newLODKey, lodCache);
            lodAsset.currentLODLevel = lodForAcquisition;
            sceneLODInfo.LODAssets.Add(lodAsset);
            // if (lodCache.TryGet(newLODKey, out var cachedAsset))
            // {
            //     //If its cached, no need to make a new promise
            //     //sceneLODInfo.SetCurrentLOD(cachedAsset, null);
            //     CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
            //     return;
            // }

            string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
            var manifest = LODUtils.LOD_MANIFESTS[newLODKey.Level];

            var assetBundleIntention = GetAssetBundleIntention.FromHash(typeof(GameObject),
                platformLODKey,
                permittedSources: AssetSource.ALL,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                manifest: manifest);

            lodAsset.LODPromise = Promise.Create(World, assetBundleIntention, partitionComponent);
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (IsLOD0(ref sceneLODInfo))
            {
                scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
                LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            }
        }

        private byte CheckLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            return sceneLODCandidate;
        }

        // private void UpdateLODLevel(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo,
        //     byte sceneLODCandidate, SceneDefinitionComponent sceneDefinitionComponent)
        // {
        //     // This function should really be called AcquireLOD()
        //     // as we store more LODs than what we display.
        //
        //     byte lodForAcquisition = sceneLODCandidate;
        //
        //     //sceneLODInfo.CurrentLODLevel = sceneLODCandidate;
        //     var newLODKey = new LODKey(sceneDefinitionComponent.Definition.id, lodForAcquisition);
        //
        //     //If the current LOD is the candidate, no need to make a new promise or set anything new
        //     if (sceneLODInfo.HasLODKey(newLODKey))
        //     {
        //         return;
        //     }
        //
        //     if (lodCache.TryGet(newLODKey, out var cachedAsset))
        //     {
        //         //If its cached, no need to make a new promise
        //         //sceneLODInfo.SetCurrentLOD(cachedAsset, null);
        //         CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
        //         return;
        //     }
        //
        //     string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
        //     var manifest = LODUtils.LOD_MANIFESTS[newLODKey.Level];
        //
        //     var assetBundleIntention =  GetAssetBundleIntention.FromHash(typeof(GameObject),
        //         platformLODKey,
        //         permittedSources: AssetSource.ALL,
        //         customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
        //         manifest: manifest);
        //
        //     byte lodPromiseArrayIndex = (byte)(sceneLODInfo.CurrentLODLevel - 1); // We're not using 0 for RAW mesh yet, so it's adjusted
        //     sceneLODInfo.CurrentLOD[lodPromiseArrayIndex].LODPromise = Promise.Create(World, assetBundleIntention, partitionComponent);
        // }

        private bool IsLOD0(ref SceneLODInfo sceneLODInfo)
        {
            //return sceneLODInfo.CurrentLOD[0].LodKey.Level == 0;
            return true;
        }
    }
}
