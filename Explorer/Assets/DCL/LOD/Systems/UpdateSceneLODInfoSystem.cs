using Arch.Core;
using DCL.SceneRunner.Scene;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
    ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveISSLODSystem))]
    [UpdateBefore(typeof(InstantiateSceneLODInfoSystem))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateSceneLODInfoSystem : BaseUnityLoopSystem
    {
        // Production LODs have always been cached under this pseudo build date; an abgen LOD source gets its own key.
        private const string REGULAR_LOD_CACHE_KEY = "dummyDate";

        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly IDecentralandUrlsSource? decentralandUrlsSource;
        private IReadOnlyList<SceneAssetBundleManifest>? manifestCache;

        public UpdateSceneLODInfoSystem(World world, ILODSettingsAsset lodSettingsAsset, IDecentralandUrlsSource? decentralandUrlsSource = null) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PortableExperienceComponent), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(ISceneFacade))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent, ISSDescriptor issDescriptor, ref SceneLoadingState sceneState)
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
                    StartLODPromise(ref sceneLODInfo, ref partitionComponent, sceneDefinitionComponent, issDescriptor, lodForAcquisition);
            }
        }

        private void StartLODPromise(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent, ISSDescriptor issDescriptor, byte level)
        {
            sceneLODInfo.ForgetAllLoadings(World);

            if (level == 0 && sceneLODInfo.InitialSceneStateLOD.CurrentState != InitialSceneStateLOD.State.Failed)
            {
                // ResolveSceneStateByIncreasingRadiusSystem gates SHOWING_LOD/SHOWING_SCENE transitions on
                // descriptor resolution, so by the time we reach this point the descriptor is guaranteed to
                // be either None (no ISS for this scene) or a resolved Bundle/Descriptor.
                if (issDescriptor.SupportsDescriptor())
                {
                    sceneLODInfo.InitialSceneStateLOD.CurrentState = InitialSceneStateLOD.State.Processing;
                    sceneLODInfo.CurrentLODLevelPromise = level;
                    return;
                }
                // descriptor in None state — no ISS for this scene; fall through to legacy LOD.
            }

            // LOD files are named by scene id only, so the Unity asset-bundle cache key must carry the LOD source.
            AssetBundleManifestVersion lodManifest = AssetBundleManifestVersion.CreateForLOD($"LOD/{level.ToString()}", decentralandUrlsSource?.AbgenLodsCacheKey ?? REGULAR_LOD_CACHE_KEY);

            // The manifest's digest-bearing name when it has one: immutable, so its cache entry survives a regeneration.
            AssetBundleManifestVersion sceneManifest = sceneDefinitionComponent.Definition.AssetBundleManifestVersionOrFailed;

            string bundleName = sceneManifest.TryGetLodBundleFile(level, out string digestNamed)
                ? digestNamed
                : $"{sceneDefinitionComponent.Definition.id.ToLower()}_{level.ToString()}";

            var assetBundleIntention = GetAssetBundleIntention.FromHash(
                lodManifest.GetCdnRequestHash(bundleName),
                lodManifest,
                typeof(GameObject),
                permittedSources: AssetSource.All,
                customEmbeddedSubDirectory: LODUtils.LOD_EMBEDDED_SUBDIRECTORIES,
                lookForDependencies: true
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
