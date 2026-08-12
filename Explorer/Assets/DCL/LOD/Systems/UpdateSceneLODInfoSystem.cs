using Arch.Core;
using DCL.SceneRunner.Scene;
using Arch.System;
using Arch.SystemGroups;
using AssetManagement;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
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
        private readonly ILODSettingsAsset lodSettingsAsset;
        private IReadOnlyList<SceneAssetBundleManifest>? manifestCache;

        // One immutable manifest + level string per possible LOD level, memoized once. The manifest is a pure function
        // of the bounded `level` byte and is only ever read downstream (never mutated — the Inject* mutators are gated
        // on flags that are false for a "LOD/{level}" manifest), so reusing them collapses a per-scene per-transition
        // AssetBundleManifestVersion allocation burst to O(distinct levels).
        private readonly AssetBundleManifestVersion[] lodManifests;
        private readonly string[] lodLevelStrings;

        // LodPartitionBucketThresholds values are runtime-mutable (quality presets and the LOD debug tools write
        // elements mid-session) but sit outside the per-scene LODEval* signature, so a frame-level snapshot comparison
        // force-reopens the gate for every scene the frame they change — applying a new threshold even to clean scenes.
        private int[] thresholdsSnapshot;
        private bool thresholdsChanged;

        public UpdateSceneLODInfoSystem(World world, ILODSettingsAsset lodSettingsAsset) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;

            int[] thresholds = lodSettingsAsset.LodPartitionBucketThresholds;
            thresholdsSnapshot = new int[thresholds.Length];
            for (int i = 0; i < thresholds.Length; i++)
                thresholdsSnapshot[i] = thresholds[i];

            int levels = thresholds.Length + 1;
            lodManifests = new AssetBundleManifestVersion[levels];
            lodLevelStrings = new string[levels];
            for (int i = 0; i < levels; i++)
            {
                lodLevelStrings[i] = i.ToString();
                AssetBundleManifestVersion manifest = AssetBundleManifestVersion.CreateForLOD($"LOD/{lodLevelStrings[i]}", "dummyDate");

                // Resolve the version-derived lazy flags once, single-threaded, so the shared instance is never lazily
                // initialised from a loader thread (SupportsDepsDigests/SupportsISS via the Inject* path, HasHashInPath
                // via GetCdnRequestPath). A "LOD/{level}" version string fails TryParseVersionNumber, so all three
                // settle deterministically false — the precondition that makes cross-scene sharing safe.
                manifest.SupportsDepsDigests();
                manifest.SupportsISS();
                manifest.HasHashInPath();

                lodManifests[i] = manifest;
            }
        }

        protected override void Update(float t)
        {
            RefreshThresholdsSnapshot();
            UpdateLODLevelQuery(World);
        }

        // Single property read per frame; sets thresholdsChanged for this frame's query pass and re-syncs the snapshot.
        private void RefreshThresholdsSnapshot()
        {
            int[] live = lodSettingsAsset.LodPartitionBucketThresholds;

            bool changed = live.Length != thresholdsSnapshot.Length;

            if (!changed)
            {
                for (int i = 0; i < live.Length; i++)
                {
                    if (live[i] != thresholdsSnapshot[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                if (live.Length != thresholdsSnapshot.Length)
                    thresholdsSnapshot = new int[live.Length];

                for (int i = 0; i < live.Length; i++)
                    thresholdsSnapshot[i] = live[i];
            }

            thresholdsChanged = changed;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PortableExperienceComponent), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(ISceneFacade))]
        private void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent, SceneDefinitionComponent sceneDefinitionComponent, ISSDescriptor issDescriptor, ref SceneLoadingState sceneState)
        {
            // Early-out when nothing this method reads has changed since this scene was last evaluated. Behaviour-preserving:
            // the skipped body's only side effect is StartLODPromise, a pure function of the inputs captured in the LODEval*
            // signature (partition via IsDirty, FullQuality, LOD-load state) plus the thresholdsChanged probe; any change
            // re-opens the gate.
            if (sceneLODInfo.LODEvalCached
                && !thresholdsChanged
                && !partitionComponent.IsDirty
                && sceneState.FullQuality == sceneLODInfo.LODEvalFullQuality
                && sceneLODInfo.metadata != null
                && sceneLODInfo.metadata.SuccessfullLODs == sceneLODInfo.LODEvalSuccessfullLODs
                && sceneLODInfo.metadata.FailedLODs == sceneLODInfo.LODEvalFailedLODs
                && sceneLODInfo.CurrentLODLevelPromise == sceneLODInfo.LODEvalLevelPromise
                && sceneLODInfo.metadata.LODChangeRelativeDistance == sceneLODInfo.LODEvalLODChangeDistance)
                return;

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

            // Record the post-evaluation input signature so the next frame can early-out while it holds.
            if (sceneLODInfo.metadata != null)
            {
                sceneLODInfo.LODEvalCached = true;
                sceneLODInfo.LODEvalFullQuality = sceneState.FullQuality;
                sceneLODInfo.LODEvalSuccessfullLODs = sceneLODInfo.metadata.SuccessfullLODs;
                sceneLODInfo.LODEvalFailedLODs = sceneLODInfo.metadata.FailedLODs;
                sceneLODInfo.LODEvalLevelPromise = sceneLODInfo.CurrentLODLevelPromise;
                sceneLODInfo.LODEvalLODChangeDistance = sceneLODInfo.metadata.LODChangeRelativeDistance;
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

            // Memoized manifests cover levels 0..LodPartitionBucketThresholds.Length (sized in the ctor). Guard against a
            // runtime thresholds growth pushing `level` past that range: fall back to an ad-hoc manifest instead of
            // indexing out of bounds. In steady state the fast path (level within range) always hits.
            AssetBundleManifestVersion lodManifest;
            string levelString;
            if (level < lodManifests.Length)
            {
                lodManifest = lodManifests[level];
                levelString = lodLevelStrings[level];
            }
            else
            {
                levelString = level.ToString();
                lodManifest = AssetBundleManifestVersion.CreateForLOD($"LOD/{levelString}", "dummyDate");
            }

            var assetBundleIntention = GetAssetBundleIntention.FromHash(
                lodManifest.GetCdnRequestHash($"{sceneDefinitionComponent.Definition.id!.ToLower()}_{levelString}"),
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
