using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utility;
using ECS.Abstract;
using System.Collections.Generic;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using DCL.SceneRunner.Scene;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(InstantiateSceneLODInfoSystem))]
    [LogCategory(ReportCategory.LOD)]
    public partial class ResolveISSLODSystem : BaseUnityLoopSystem
    {
        private readonly IGltfContainerAssetsCache gltfCache;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        internal ResolveISSLODSystem(World world, IGltfContainerAssetsCache gltfCache, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.gltfCache = gltfCache;
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ResolveInitialSceneStateLODDescriptorQuery(World);
            ConvertFromAssetBundleQuery(World);
        }

        [Query]
        private void ResolveInitialSceneStateLODDescriptor(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinition, ISSDescriptor issDescriptor)
        {
            InitialSceneStateLOD initialSceneStateLOD = sceneLODInfo.InitialSceneStateLOD;

            if (initialSceneStateLOD.CurrentState != InitialSceneStateLOD.State.PROCESSING) return;

            // Only the descriptor-mode path is owned by this query.
            if (issDescriptor.CurrentState != ISSDescriptorState.Descriptor) return;

            // First-time entry: nothing to do once promises have been spawned.
            if (initialSceneStateLOD.ParentContainer != null) return;

            IReadOnlyList<ISSDescriptorAsset> assets = issDescriptor.Assets;

            initialSceneStateLOD.InitializeFromDescriptor(sceneLODInfo.id, sceneDefinition.SceneGeometry.BaseParcelPosition,
                gltfCache, assets.Count);

            SpawnAssetPromises(initialSceneStateLOD, assets, sceneDefinition, issDescriptor);
        }

        /// <summary>
        ///     For each entry in the descriptor either positions a cached asset immediately or spawns
        ///     a new <see cref="AssetBundlePromise"/> for it.
        /// </summary>
        private void SpawnAssetPromises(InitialSceneStateLOD initialSceneStateLOD, IReadOnlyList<ISSDescriptorAsset> assets, SceneDefinitionComponent sceneDefinition, ISSDescriptor issDescriptor)
        {
            AssetBundleManifestVersion? manifest = sceneDefinition.Definition.assetBundleManifestVersion;

            for (var i = 0; i < assets.Count; i++)
            {
                ISSDescriptorAsset entry = assets[i];

                // The GLTF container cache is keyed by "hash@digest" (see AssetBundleManifestVersionExtensions.ComposeCacheKey).
                // Looking up by bare hash misses any bridged entry the SDK runtime left behind, so the LOD spawns a
                // second instance of an asset that's already resident — that's the visible "double" overlap.
                string cacheKey = manifest.ComposeCacheKey(entry.hash);

                if (gltfCache.TryGet(cacheKey, out var asset))
                {
                    // Best-effort release: if this hit came from a prior SDK→LOD bridge handoff there's
                    // a slot to free; on the first-ever LOD load of a scene the cache may have the asset
                    // from an unrelated context with no reservation, in which case this is a no-op.
                    issDescriptor.TryReleaseBridgeSlot(entry.hash);
                    PositionAsset(initialSceneStateLOD, entry, cacheKey, asset, initialSceneStateLOD.ParentContainer.transform);
                    continue;
                }

                // Descriptor mode: fetch each asset's own bundle — must include the platform suffix to match the deployed AB filename.
                string promiseHash = $"{entry.hash}{PlatformUtils.GetCurrentPlatform()}";

                var intent = GetAssetBundleIntention.FromHash(promiseHash,
                    assetBundleManifestVersion: manifest,
                    parentEntityID: sceneDefinition.Definition.id);

                // Mirror the digest populated by PrepareGltfAssetLoadingSystem so this promise lands in the same
                // AssetBundleCache slot as the SDK runtime would. Without it the (Hash, DepsDigest) key diverges
                // and two parallel LoadAssetBundleSystem flows race for the same physical bundle, which Unity
                // refuses with "asset bundle already loaded". The digest map is keyed by bare CID.
                if (manifest != null && manifest.TryGetDepsDigest(entry.hash, out string digest))
                    intent.DepsDigest = digest;

                AssetBundlePromise promise = AssetBundlePromise.Create(World, intent, PartitionComponent.TOP_PRIORITY);

                ISSAssetCreationHelper assetCreationHelper = new ISSAssetCreationHelper(initialSceneStateLOD, entry, cacheKey);

                World.Create(promise, assetCreationHelper);
            }
        }

        [Query]
        private void ConvertFromAssetBundle(Entity entity, ISSAssetCreationHelper creationHelper, ref AssetBundlePromise assetBundleResult)
        {
            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (!assetBundleResult.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
                return;

            bool stillRelevant = creationHelper.Generation == creationHelper.InitialSceneStateLOD.Generation
                                 && creationHelper.InitialSceneStateLOD.ParentContainer != null;

            if (Result.Succeeded)
            {
                if (stillRelevant)
                {
                    if (Utils.TryCreateGltfObject(Result.Asset, creationHelper.AssetNameInBundle, out GltfContainerAsset asset))
                    {
                        PositionAsset(creationHelper.InitialSceneStateLOD, creationHelper.Entry, creationHelper.CacheKey, asset,
                            creationHelper.InitialSceneStateLOD.ParentContainer.transform);
                    }
                    else
                    {
                        ReportHub.LogWarning(GetReportData(), $"Failed to load {creationHelper.Entry.hash} for LOD, the result may not look correct");
                        creationHelper.InitialSceneStateLOD.AddFailedAsset(creationHelper.Entry.hash);
                    }
                }
                else
                {
                    //Means that the ISS loading has been cancelled. We need to remove the reference to keep counting correctly
                    Result.Asset!.Dereference();
                }
            }
            else if (stillRelevant)
            {
                // AB promise failed (e.g. 404 / network). Count it so AllAssetsInstantiated can settle
                // and UnloadLODForISS gets a chance to bridge the successful assets.
                creationHelper.InitialSceneStateLOD.AddFailedAsset(creationHelper.Entry.hash);
            }

            World.Destroy(entity);
        }


        private static void PositionAsset(InitialSceneStateLOD initialSceneStateLOD, ISSDescriptorAsset entry, string cacheKey, GltfContainerAsset asset, Transform parent)
        {
            asset.Root.SetActive(true);
            asset.Root.transform.SetParent(parent);
            asset.Root.transform.localPosition = entry.position;
            asset.Root.transform.localRotation = entry.rotation;
            asset.Root.transform.localScale = entry.scale;

            asset.ToggleAnimationState(false);

            // Store under the digest-aware cache key so the eventual Dereference in InitialSceneStateLOD.Clear
            // matches what the SDK runtime would look up — that's what allows bridging round-trips between
            // LOD and the real scene without spawning a second copy of the same asset.
            initialSceneStateLOD.AddResolvedAsset(cacheKey, asset);
        }

    }

    public struct ISSAssetCreationHelper
    {
        public ISSAssetCreationHelper(InitialSceneStateLOD initialSceneStateLOD, ISSDescriptorAsset entry, string cacheKey)
        {
            InitialSceneStateLOD = initialSceneStateLOD;
            Entry = entry;
            // Bundle mode: the shared ISS bundle contains many assets keyed by hash.
            // Descriptor mode: per-asset bundle has a single asset, so passing empty name to TryGetAsset returns it.
            // Both shared ISS bundles and per-asset bundles are baked with assets named by their content hash.
            AssetNameInBundle = entry.hash;
            CacheKey = cacheKey;
            Generation = initialSceneStateLOD.Generation;
        }

        public InitialSceneStateLOD InitialSceneStateLOD { get; }
        public ISSDescriptorAsset Entry { get; }
        public string AssetNameInBundle { get; }
        public string CacheKey { get; }
        public int Generation { get; }
    }
}
