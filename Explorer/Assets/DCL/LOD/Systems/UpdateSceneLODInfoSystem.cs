﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private IReadOnlyList<SceneAssetBundleManifest>? manifestCache;

        public UpdateSceneLODInfoSystem(World world, ILODSettingsAsset lodSettingsAsset, IDecentralandUrlsSource decentralandUrlsSource) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PortableExperienceComponent))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent, ref SceneLoadingState sceneState)
        {
            if (!partitionComponent.IsBehind) // Only want to load scene in our direction of travel && not quality reducted
            {
                byte lodForAcquisition;

                //If we are quality reducted, always load the greated value LOD
                if (!sceneState.FullQuality)
                    lodForAcquisition = (byte)lodSettingsAsset.LodPartitionBucketThresholds.Length;
                else
                {
                    // LOD distances are currently using the old system so will only load in the LOD when the gameobject
                    // is in the correct bucket. Once the lods are in it will change LODs based on screenspace size in relation
                    // to height and dither the transition.
                    lodForAcquisition = GetLODLevelForPartition(ref partitionComponent, ref sceneLODInfo);
                }
                if (!sceneLODInfo.HasLOD(lodForAcquisition))
                    StartLODPromise(ref sceneLODInfo, ref partitionComponent, sceneDefinitionComponent, lodForAcquisition);
            }
        }

        private void StartLODPromise(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent, byte level)
        {
            sceneLODInfo.CurrentLODPromise.ForgetLoading(World);

            string platformLODKey = $"{sceneDefinitionComponent.Definition.id.ToLower()}_{level.ToString()}{PlatformUtils.GetCurrentPlatform()}";
            var manifest = LODUtils.LODManifests(decentralandUrlsSource)[level];

            var assetBundleIntention = GetAssetBundleIntention.FromHash(typeof(GameObject),
                platformLODKey,
                permittedSources: AssetSource.ALL,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                manifest: manifest,
                lookForShaderAsset: true
                );

            sceneLODInfo.CurrentLODPromise = Promise.Create(World, assetBundleIntention, partitionComponent);
            sceneLODInfo.CurrentLODLevelPromise = level;
        }

        private byte GetLODLevelForPartition(ref PartitionComponent partitionComponent, ref SceneLODInfo sceneLODInfo)
        {
            //If we are in an SDK6 scene, this value will be kept.
            //Therefore, lod0 will be shown
            byte sceneLODCandidate = 0;

            for (byte i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                if (partitionComponent.Bucket >= lodSettingsAsset.LodPartitionBucketThresholds[i])
                    sceneLODCandidate = (byte)(i + 1);
            }

            //LOD0 load distance may be very far away from its show distance depending on the object size.
            //So, we force it if it has not been loaded and we passed the show distance threshold
            if (sceneLODInfo.metadata.LODChangeRelativeDistance >= partitionComponent.Bucket * ParcelMathHelper.PARCEL_SIZE
                && sceneLODCandidate == 1 && !sceneLODInfo.HasLOD(0))
                sceneLODCandidate = 0;

            return sceneLODCandidate;
        }
    }
}
