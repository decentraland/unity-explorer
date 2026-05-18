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
        private IGltfContainerAssetsCache gltfCache;
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        public ResolveISSLODSystem(World world, IGltfContainerAssetsCache gltfCache, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.gltfCache = gltfCache;
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ResolveInitialSceneStateLODBundleQuery(World);
            ResolveInitialSceneStateLODDescriptorQuery(World);
            ConvertFromAssetBundleQuery(World);
        }

        [Query]
        private void ResolveInitialSceneStateLODBundle(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinition)
        {
            InitialSceneStateLOD initialSceneStateLOD = sceneLODInfo.InitialSceneStateLOD;

            if (initialSceneStateLOD.CurrentState != InitialSceneStateLOD.State.PROCESSING) return;

            // Only the bundle-mode path is owned by this query.
            ISSDescriptor issDescriptor = sceneDefinition.Definition.ISSDescriptor;
            if (issDescriptor.CurrentState != ISSDescriptor.State.Bundle) return;

            // Skip if promise hasn't been created yet or is already consumed
            if (initialSceneStateLOD.AssetBundlePromise == AssetBundlePromise.NULL || initialSceneStateLOD.AssetBundlePromise.IsConsumed) return;

            if (!initialSceneStateLOD.AssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
                return;

            if (!Result.Succeeded)
            {
                MarkAssetBundleAsFailed(ref sceneLODInfo,
                    $"Failed to get ISS LOD for  {sceneLODInfo.id}, will try to do the old LOD");
                return;
            }

            IReadOnlyList<ISSDescriptorAsset> assets = issDescriptor.Assets;

            initialSceneStateLOD.Initialize(sceneLODInfo.id, sceneDefinition.SceneGeometry.BaseParcelPosition, Result.Asset!,
                gltfCache, assets.Count);

            SpawnAssetPromises(initialSceneStateLOD, assets, sceneDefinition, fromBundle: true);
        }

        [Query]
        private void ResolveInitialSceneStateLODDescriptor(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinition)
        {
            InitialSceneStateLOD initialSceneStateLOD = sceneLODInfo.InitialSceneStateLOD;

            if (initialSceneStateLOD.CurrentState != InitialSceneStateLOD.State.PROCESSING) return;

            // Only the descriptor-mode path is owned by this query.
            ISSDescriptor issDescriptor = sceneDefinition.Definition.ISSDescriptor;
            if (issDescriptor.CurrentState != ISSDescriptor.State.Descriptor) return;

            // First-time entry: nothing to do once promises have been spawned.
            if (initialSceneStateLOD.ParentContainer != null) return;

            IReadOnlyList<ISSDescriptorAsset> assets = issDescriptor.Assets;

            initialSceneStateLOD.InitializeFromDescriptor(sceneLODInfo.id, sceneDefinition.SceneGeometry.BaseParcelPosition,
                gltfCache, assets.Count);

            SpawnAssetPromises(initialSceneStateLOD, assets, sceneDefinition, fromBundle: false);
        }

        /// <summary>
        ///     For each entry in the descriptor either positions a cached asset immediately or spawns
        ///     a new <see cref="AssetBundlePromise"/> for it. Used by both the bundle and the descriptor paths;
        ///     <paramref name="fromBundle"/> picks the per-asset bundle URL and the asset name to extract.
        /// </summary>
        private void SpawnAssetPromises(InitialSceneStateLOD initialSceneStateLOD, IReadOnlyList<ISSDescriptorAsset> assets, SceneDefinitionComponent sceneDefinition, bool fromBundle)
        {
            for (var i = 0; i < assets.Count; i++)
            {
                ISSDescriptorAsset entry = assets[i];

                if (gltfCache.TryGet(entry.hash, out var asset))
                {
                    // We just consumed one bridged copy — free up its slot so a future SDK cleanup of the same hash can re-bridge.
                    sceneDefinition.Definition.ISSDescriptor.ReleaseBridgeSlot(entry.hash);
                    PositionAsset(initialSceneStateLOD, entry, asset, initialSceneStateLOD.ParentContainer.transform);
                    continue;
                }

                // Bundle mode: refetch the shared ISS bundle (cached) so ref counting is correct.
                // Descriptor mode: fetch each asset's own bundle — must include the platform suffix to match the deployed AB filename.
                string promiseHash = fromBundle
                    ? GetAssetBundleIntention.BuildInitialSceneStateURL(sceneDefinition.Definition.id)
                    : $"{entry.hash}{PlatformUtils.GetCurrentPlatform()}";

                AssetBundlePromise promise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.FromHash(promiseHash,
                        assetBundleManifestVersion: sceneDefinition.Definition.assetBundleManifestVersion,
                        parentEntityID: sceneDefinition.Definition.id),
                    PartitionComponent.TOP_PRIORITY);

                ISSAssetCreationHelper assetCreationHelper = new ISSAssetCreationHelper(initialSceneStateLOD, entry);

                World.Create(promise, assetCreationHelper);
            }
        }

        [Query]
        private void ConvertFromAssetBundle(Entity entity, ISSAssetCreationHelper creationHelper, ref AssetBundlePromise assetBundleResult)
        {
            const string DEBUG_SCENE_ID = "bafkreift34mmemx7fvrf6mpoaab7qy2dceq5vwpwehq3wunv5dwulbjveu";

            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (!assetBundleResult.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
                return;

            bool isDebugScene = creationHelper.InitialSceneStateLOD.SceneID == DEBUG_SCENE_ID;
            bool stillRelevant = creationHelper.Generation == creationHelper.InitialSceneStateLOD.Generation
                                 && creationHelper.InitialSceneStateLOD.ParentContainer != null;

            if (Result.Succeeded)
            {
                if (stillRelevant)
                {
                    if (Utils.TryCreateGltfObject(Result.Asset, creationHelper.AssetNameInBundle, out GltfContainerAsset asset))
                    {
                        if (isDebugScene)
                            UnityEngine.Debug.Log($"[Juani] ConvertFromAssetBundle OK {creationHelper.Entry.hash} (counted via AddResolvedAsset)");
                        PositionAsset(creationHelper.InitialSceneStateLOD, creationHelper.Entry, asset,
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


        private static void PositionAsset(InitialSceneStateLOD initialSceneStateLOD, ISSDescriptorAsset entry, GltfContainerAsset asset, Transform parent)
        {
            asset.Root.SetActive(true);
            asset.Root.transform.SetParent(parent);
            asset.Root.transform.localPosition = entry.position;
            asset.Root.transform.localRotation = entry.rotation;
            asset.Root.transform.localScale = entry.scale;

            asset.ToggleAnimationState(false);

            initialSceneStateLOD.AddResolvedAsset(entry.hash, asset);
        }

        private static void MarkAssetBundleAsFailed(ref SceneLODInfo sceneLODInfo, string message)
        {
            ReportHub.Log(ReportCategory.LOD, message);
            sceneLODInfo.InitialSceneStateLOD.CurrentState = InitialSceneStateLOD.State.FAILED;
            //We need to re-evaluate the LOD to see if we can get the old method
            sceneLODInfo.CurrentLODLevelPromise = byte.MaxValue;
        }

    }

    public struct ISSAssetCreationHelper
    {
        public ISSAssetCreationHelper(InitialSceneStateLOD initialSceneStateLOD, ISSDescriptorAsset entry)
        {
            InitialSceneStateLOD = initialSceneStateLOD;
            Entry = entry;
            // Bundle mode: the shared ISS bundle contains many assets keyed by hash.
            // Descriptor mode: per-asset bundle has a single asset, so passing empty name to TryGetAsset returns it.
            // Both shared ISS bundles and per-asset bundles are baked with assets named by their content hash.
            AssetNameInBundle = entry.hash;
            Generation = initialSceneStateLOD.Generation;
        }

        public InitialSceneStateLOD InitialSceneStateLOD { get; }
        public ISSDescriptorAsset Entry { get; }
        public string AssetNameInBundle { get; }
        public int Generation { get; }
    }
}
