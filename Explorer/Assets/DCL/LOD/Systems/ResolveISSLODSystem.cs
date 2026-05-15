using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
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
            ISSDescriptor? issDescriptor = sceneDefinition.Definition.ISSDescriptor;
            if (issDescriptor == null || issDescriptor.CurrentState != ISSDescriptor.State.Bundle) return;

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

            if (!issDescriptor.Metadata.HasValue)
            {
                MarkAssetBundleAsFailed(ref sceneLODInfo,
                    $"No initial scene state descriptor available for {sceneLODInfo.id}, will try to do the old LOD");
                Result.Asset!.Dereference();
                return;
            }

            ISSDescriptorMetadata metadata = issDescriptor.Metadata.Value;
            int count = metadata.assets?.Count ?? 0;

            initialSceneStateLOD.Initialize(sceneLODInfo.id, sceneDefinition.SceneGeometry.BaseParcelPosition, Result.Asset!,
                gltfCache, count);

            SpawnAssetPromises(initialSceneStateLOD, metadata, sceneDefinition, fromBundle: true);
        }

        [Query]
        private void ResolveInitialSceneStateLODDescriptor(ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinition)
        {
            InitialSceneStateLOD initialSceneStateLOD = sceneLODInfo.InitialSceneStateLOD;

            if (initialSceneStateLOD.CurrentState != InitialSceneStateLOD.State.PROCESSING) return;

            // Only the descriptor-mode path is owned by this query.
            ISSDescriptor? issDescriptor = sceneDefinition.Definition.ISSDescriptor;
            if (issDescriptor == null || issDescriptor.CurrentState != ISSDescriptor.State.Descriptor) return;
            if (!issDescriptor.Metadata.HasValue) return;

            // First-time entry: nothing to do once promises have been spawned.
            if (initialSceneStateLOD.ParentContainer != null) return;

            ISSDescriptorMetadata metadata = issDescriptor.Metadata.Value;
            int count = metadata.assets?.Count ?? 0;

            initialSceneStateLOD.InitializeFromDescriptor(sceneLODInfo.id, sceneDefinition.SceneGeometry.BaseParcelPosition,
                gltfCache, count);

            SpawnAssetPromises(initialSceneStateLOD, metadata, sceneDefinition, fromBundle: false);
        }

        /// <summary>
        ///     For each entry in the descriptor either positions a cached asset immediately or spawns
        ///     a new <see cref="AssetBundlePromise"/> for it. Used by both the bundle and the descriptor paths;
        ///     <paramref name="fromBundle"/> picks the per-asset bundle URL and the asset name to extract.
        /// </summary>
        private void SpawnAssetPromises(InitialSceneStateLOD initialSceneStateLOD, ISSDescriptorMetadata metadata, SceneDefinitionComponent sceneDefinition, bool fromBundle)
        {
            if (metadata.assets == null) return;

            for (var i = 0; i < metadata.assets.Count; i++)
            {
                ISSDescriptorAsset entry = metadata.assets[i];

                if (gltfCache.TryGet(entry.hash, out var asset))
                {
                    PositionAsset(initialSceneStateLOD, entry, asset, initialSceneStateLOD.ParentContainer.transform);
                    continue;
                }

                // Bundle mode: refetch the shared ISS bundle (cached) so ref counting is correct.
                // Descriptor mode: fetch each asset's own bundle by its hash.
                string promiseHash = fromBundle
                    ? GetAssetBundleIntention.BuildInitialSceneStateURL(sceneDefinition.Definition.id)
                    : entry.hash;

                AssetBundlePromise promise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.FromHash(promiseHash,
                        assetBundleManifestVersion: sceneDefinition.Definition.assetBundleManifestVersion,
                        parentEntityID: sceneDefinition.Definition.id),
                    PartitionComponent.TOP_PRIORITY);

                ISSAssetCreationHelper assetCreationHelper = new ISSAssetCreationHelper(initialSceneStateLOD, entry, fromBundle);

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

            if (Result.Succeeded)
            {
                if (creationHelper.Generation == creationHelper.InitialSceneStateLOD.Generation
                    && creationHelper.InitialSceneStateLOD.ParentContainer != null)
                {
                    if (Utils.TryCreateGltfObject(Result.Asset, creationHelper.AssetNameInBundle, isPartOfISS: true, out GltfContainerAsset asset))
                        PositionAsset(creationHelper.InitialSceneStateLOD, creationHelper.Entry, asset,
                            creationHelper.InitialSceneStateLOD.ParentContainer.transform);
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
        public ISSAssetCreationHelper(InitialSceneStateLOD initialSceneStateLOD, ISSDescriptorAsset entry, bool fromBundle)
        {
            InitialSceneStateLOD = initialSceneStateLOD;
            Entry = entry;
            // Bundle mode: the shared ISS bundle contains many assets keyed by hash.
            // Descriptor mode: per-asset bundle has a single asset, so passing empty name to TryGetAsset returns it.
            AssetNameInBundle = fromBundle ? entry.hash : string.Empty;
            Generation = initialSceneStateLOD.Generation;
        }

        public InitialSceneStateLOD InitialSceneStateLOD { get; }
        public ISSDescriptorAsset Entry { get; }
        public string AssetNameInBundle { get; }
        public int Generation { get; }
    }
}
