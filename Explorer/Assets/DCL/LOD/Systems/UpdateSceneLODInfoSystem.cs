﻿using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
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
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private IReadOnlyList<SceneAssetBundleManifest>? manifestCache;
        private IReadOnlyList<SceneAssetBundleManifest> LODManifests => manifestCache ??= LODUtils.LODManifests(decentralandUrlsSource);

        public UpdateSceneLODInfoSystem(
            World world,
            ILODAssetsPool lodCache,
            ILODSettingsAsset lodSettingsAsset,
            IScenesCache scenesCache,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            IDecentralandUrlsSource decentralandUrlsSource
        ) : base(world)
        {
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PortableExperienceComponent))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent)
        {
            //New LOD infront of you. Update
            if (!partitionComponent.IsBehind && sceneLODInfo.CurrentLODLevel == byte.MaxValue)
            {
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
                return;
            }

            //Existing LOD (either infront or behind you). Update
            if (partitionComponent.IsDirty && sceneLODInfo.CurrentLODLevel != byte.MaxValue)
                CheckLODLevel(ref partitionComponent, ref sceneLODInfo, sceneDefinitionComponent);
        }

        private void CheckSceneReadinessAndClean(ref SceneLODInfo sceneLODInfo, SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (IsLOD0(ref sceneLODInfo))
            {
                scenesCache.AddNonRealScene(sceneDefinitionComponent.Parcels);
                LODUtils.CheckSceneReadiness(sceneReadinessReportQueue, sceneDefinitionComponent);
            }

            sceneLODInfo.IsDirty = false;
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

            if (newLODKey.Equals(sceneLODInfo.CurrentVisibleLOD))
            {
                sceneLODInfo.ResetToCurrentVisibleLOD();
                sceneLODInfo.IsDirty = false;
                return;
            }

            if (lodCache.TryGet(newLODKey, out LODAsset? cachedAsset))
            {
                //If its cached, no need to make a new promise
                sceneLODInfo.SetCurrentLOD(cachedAsset);
                CheckSceneReadinessAndClean(ref sceneLODInfo, sceneDefinitionComponent);
                return;
            }

            string platformLODKey = newLODKey + PlatformUtils.GetPlatform();
            var manifest = LODManifests[newLODKey.Level];

            var assetBundleIntention = GetAssetBundleIntention.FromHash(typeof(GameObject),
                platformLODKey,
                permittedSources: AssetSource.ALL,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                manifest: manifest);

            sceneLODInfo.CurrentLODPromise =
                Promise.Create(World, assetBundleIntention, partitionComponent);

            sceneLODInfo.IsDirty = true;
        }

        private static bool IsLOD0(ref SceneLODInfo sceneLODInfo) =>
            sceneLODInfo.CurrentLOD.LodKey.Level == 0;
    }
}
