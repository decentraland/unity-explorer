﻿using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System;
using System.Threading;
using AssetManagement;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Cache.Disk;
using Google.Protobuf;
using System.Buffers;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleSystem : LoadSystemBase<AssetBundleData, GetAssetBundleIntention>
    {
        private const string METADATA_FILENAME = "metadata.json";
        private const string STATIC_SCENE_DESCRIPTOR_FILENAME = "StaticSceneDescriptor.json";
        private static readonly ThreadSafeObjectPool<AssetBundleMetadata> METADATA_POOL
            = new (() => new AssetBundleMetadata(),
                actionOnRelease: metadata => metadata.Clear()
              , maxSize: 100);

        private readonly AssetBundleLoadingMutex loadingMutex;
        private readonly IWebRequestController webRequestController;

        internal LoadAssetBundleSystem(World world,
            IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache,
            IWebRequestController webRequestController,
            ArrayPool<byte> buffersPool,
            AssetBundleLoadingMutex loadingMutex,
            IDiskCache<PartialLoadingState> partialDiskCache) : base(world, cache)
        {
            this.loadingMutex = loadingMutex;
            this.webRequestController = webRequestController;
        }

        private async UniTask<AssetBundleData[]> LoadDependenciesAsync(GetAssetBundleIntention parentIntent, IPartitionComponent partition, AssetBundleMetadata assetBundleMetadata, CancellationToken ct)
        {
            // Construct dependency promises and wait for them
            // Switch to main thread to create dependency promises
            await UniTask.SwitchToMainThread();

            URLSubdirectory customEmbeddedSubdirectory = parentIntent.CommonArguments.CustomEmbeddedSubDirectory;

            return await UniTask.WhenAll(assetBundleMetadata.dependencies.Select(hash => WaitForDependencyAsync(hash, parentIntent.AssetBundleManifestVersion!, parentIntent.ParentEntityID, customEmbeddedSubdirectory, partition, ct)));
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> FlowInternalAsync(GetAssetBundleIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            AssetBundleLoadingResult assetBundleResult = await webRequestController
               .GetAssetBundleAsync(intention.CommonArguments, new GetAssetBundleArguments(loadingMutex, intention.cacheHash), ct, GetReportCategory(),
                    suppressErrors: true); // Suppress errors because here we have our own error handling

            AssetBundle? assetBundle = assetBundleResult.AssetBundle;

            // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
            state.AcquiredBudget!.Release();

            // if GetContent prints an error, null will be thrown
            if (assetBundle == null)
                throw new NullReferenceException($"{intention.Hash} Asset Bundle is null: {assetBundleResult.DataProcessingError}");

            try
            {
                // get metrics

                string? metadataJSON;
                string? sceneDescriptoJSON;


                using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                {
                    metadataJSON = assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME)?.text;
                    sceneDescriptoJSON = assetBundle.LoadAsset<TextAsset>(STATIC_SCENE_DESCRIPTOR_FILENAME)?.text;
                }

                // Switch to thread pool to parse JSONs

                await UniTask.SwitchToThreadPool();
                ct.ThrowIfCancellationRequested();

                AssetBundleData[] dependencies;
                var mainAsset = "";
                InitialSceneStateMetadata? initialSceneState = null;

                if (!string.IsNullOrEmpty(sceneDescriptoJSON))
                    initialSceneState = JsonUtility.FromJson<InitialSceneStateMetadata>(sceneDescriptoJSON);

                if (!string.IsNullOrEmpty(metadataJSON))
                {
                    using PoolExtensions.Scope<AssetBundleMetadata> reusableMetadata = METADATA_POOL.AutoScope();

                    // Parse metadata
                    JsonUtility.FromJsonOverwrite(metadataJSON, reusableMetadata.Value);
                    mainAsset = reusableMetadata.Value.mainAsset;
                    dependencies = await LoadDependenciesAsync(intention, partition, reusableMetadata.Value, ct);
                }
                else
                    dependencies = Array.Empty<AssetBundleData>();

                ct.ThrowIfCancellationRequested();

                string source = intention.CommonArguments.CurrentSource.ToStringNonAlloc();

                // if the type was not specified don't load any assets
                return await CreateAssetBundleDataAsync(assetBundle, initialSceneState, intention.ExpectedObjectType, mainAsset, loadingMutex, dependencies, GetReportData(),
                    intention.AssetBundleManifestVersion == null ? "" : intention.AssetBundleManifestVersion.GetAssetBundleManifestVersion(),
                    source, intention.IsDependency, ct);
            }
            catch (Exception e)
            {
                // If the loading process didn't finish successfully unload the bundle
                // Otherwise, it gets stuck in Unity's memory but not cached in our cache
                // Can only be done in main thread
                await UniTask.SwitchToMainThread();

                if (assetBundle)
                    assetBundle.Unload(true);

                throw;
            }
        }

        public static async UniTask<StreamableLoadingResult<AssetBundleData>> CreateAssetBundleDataAsync(
            AssetBundle assetBundle, InitialSceneStateMetadata? initialSceneState, Type? expectedObjType, string? mainAsset,
            AssetBundleLoadingMutex loadingMutex,
            AssetBundleData[] dependencies,
            ReportData reportCategory,
            string version,
            string source,
            bool isDependency,
            CancellationToken ct)
        {
            if (isDependency)
                return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, dependencies));

            if (expectedObjType == typeof(GameObject))
            {
                //If there are no dependencies, it means that this gameobject asset bundle has the shader in it.
                //All gameobject asset bundles ahould at least have the dependency on the shader.
                //This will cause a material leak, as the same material will be loaded again. This needs to be solved at asset bundle level
                if (dependencies.Length == 0)
                    throw new StreamableLoadingException(LogType.Warning, nameof(LoadAssetBundleSystem), new AssetBundleContainsShaderException(assetBundle.name));
            }

            Object[]? asset = await LoadAllAssetsAsync(assetBundle, expectedObjType, mainAsset, loadingMutex, reportCategory, ct);

            return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, initialSceneState, asset, expectedObjType, dependencies,
                version: version,
                source: source));
        }

        private static async UniTask<Object[]> LoadAllAssetsAsync(AssetBundle assetBundle, Type? objectType, string? mainAsset, AssetBundleLoadingMutex loadingMutex, ReportData reportCategory, CancellationToken ct)
        {
            using AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct);

            AssetBundleRequest? asyncOp;

            if(!string.IsNullOrEmpty(mainAsset))
                asyncOp = assetBundle.LoadAssetAsync(mainAsset);
            else if(objectType != null)
                asyncOp = assetBundle.LoadAllAssetsAsync(objectType);
            else
            //If no asset type or name was specified, we need to load all
                asyncOp = assetBundle.LoadAllAssetsAsync();

            await asyncOp.WithCancellation(ct);
            Object[]? assets = asyncOp.allAssets;

            return assets;
        }

        private async UniTask<AssetBundleData> WaitForDependencyAsync(
            string hash,
            AssetBundleManifestVersion assetBundleManifestVersion,
            string parentEntityID,
            URLSubdirectory customEmbeddedSubdirectory,
            IPartitionComponent partition, CancellationToken ct)
        {
            // Inherit partition from the parent promise
            // we don't know the type of the dependency
            var assetBundlePromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, GetAssetBundleIntention.FromHash(hash, assetBundleManifestVersion: assetBundleManifestVersion, parentEntityID: parentEntityID, customEmbeddedSubDirectory: customEmbeddedSubdirectory, isDependency : true), partition);

            try
            {
                assetBundlePromise = await assetBundlePromise.ToUniTaskAsync(World, cancellationToken: ct);

                if (!assetBundlePromise.TryGetResult(World, out StreamableLoadingResult<AssetBundleData> depResult))
                    throw new Exception($"Dependency {hash} is not resolved");

                if (!depResult.Succeeded)
                    throw new Exception($"Dependency {hash} resolution failed", depResult.Exception);

                return depResult.Asset;
            }
            catch (OperationCanceledException)
            {
                assetBundlePromise.ForgetLoading(World);
                throw new OperationCanceledException($"Dependency {hash} resolution cancelled");
            }
        }
    }
}
