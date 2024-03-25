using Arch.Core;
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
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadAssetBundleSystem : LoadSystemBase<AssetBundleData, GetAssetBundleIntention>
    {
        private const string METADATA_FILENAME = "metadata.json";
        private const string METRICS_FILENAME = "metrics.json";
        private static readonly ThreadSafeObjectPool<AssetBundleMetadata> METADATA_POOL
            = new (() => new AssetBundleMetadata(), maxSize: 100);

        private readonly AssetBundleLoadingMutex loadingMutex;

        internal LoadAssetBundleSystem(World world,
            IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache,
            MutexSync mutexSync,
            AssetBundleLoadingMutex loadingMutex) : base(world, cache, mutexSync)
        {
            this.loadingMutex = loadingMutex;
        }

        private async UniTask<AssetBundleData[]> LoadDependenciesAsync(GetAssetBundleIntention parentIntent, IPartitionComponent partition, AssetBundle assetBundle, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            string? metadata;

            using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                metadata = GetMetadata(assetBundle)?.text;

            if (metadata != null)
            {
                using PoolExtensions.Scope<AssetBundleMetadata> reusableMetadata = METADATA_POOL.AutoScope();

                // Parse metadata
                JsonUtility.FromJsonOverwrite(metadata, reusableMetadata.Value);

                // Construct dependency promises and wait for them
                // Switch to main thread to create dependency promises
                await UniTask.SwitchToMainThread();

                var manifest = parentIntent.Manifest;
                var customEmbeddedSubdirectory = parentIntent.CommonArguments.CustomEmbeddedSubDirectory;
                var expectedObjectType = parentIntent.ExpectedObjectType;

                return await UniTask.WhenAll(reusableMetadata.Value.dependencies.Select(hash => WaitForDependencyAsync(manifest, hash, customEmbeddedSubdirectory, partition, ct)));
            }

            return Array.Empty<AssetBundleData>();
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> FlowInternalAsync(GetAssetBundleIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            AssetBundle assetBundle;

            using (UnityWebRequest webRequest = intention.cacheHash.HasValue
                       ? UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL, intention.cacheHash.Value)
                       : UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL))
            {
                ((DownloadHandlerAssetBundle)webRequest.downloadHandler).autoLoadAssetBundle = false;
                await webRequest.SendWebRequest().WithCancellation(ct);

                using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                    assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest);

                // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
                acquiredBudget.Release();

                // if GetContent prints an error, null will be thrown
                if (assetBundle == null)
                    throw new NullReferenceException($"{intention.Hash} Asset Bundle is null: {webRequest.downloadHandler.error}");
            }

            try
            {
                // get metrics

                TextAsset metricsFile;

                using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                    metricsFile = assetBundle.LoadAsset<TextAsset>(METRICS_FILENAME);

                // Switch to thread pool to parse JSONs

                await UniTask.SwitchToThreadPool();
                ct.ThrowIfCancellationRequested();

                AssetBundleMetrics? metrics = metricsFile != null ? JsonUtility.FromJson<AssetBundleMetrics>(metricsFile.text) : null;

                AssetBundleData[] dependencies = await LoadDependenciesAsync(intention, partition, assetBundle, ct);

                await UniTask.SwitchToMainThread();
                ct.ThrowIfCancellationRequested();

                // if the type was not specified don't load any assets
                if (intention.ExpectedObjectType == null)
                    return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, dependencies));

                var asset = await LoadAllAssetsAsync(assetBundle, intention.ExpectedObjectType, ct);
                return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, asset, intention.ExpectedObjectType, dependencies));
            }
            catch (Exception)
            {
                // If the loading process didn't finish successfully unload the bundle
                // Otherwise, it gets stuck in Unity's memory but not cached in our cache
                if (assetBundle)
                    assetBundle.Unload(true);

                throw;
            }
        }

        protected override void OnAssetSuccessfullyLoaded(AssetBundleData asset) =>
            asset.AddReference();

        private async UniTask<Object> LoadAllAssetsAsync(AssetBundle assetBundle, Type objectType, CancellationToken ct) {
            using AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct);

            // we are only interested in game objects
            AssetBundleRequest asyncOp = assetBundle.LoadAllAssetsAsync(objectType);
            await asyncOp.WithCancellation(ct);

            var assets = asyncOp.allAssets;

            switch (assets.Length)
            {
                case 0:
                    throw new AssetBundleMissingMainAssetException(assetBundle.name, objectType);
                case > 1:
                    ReportHub.LogError(GetReportCategory(), $"AssetBundle {assetBundle.name} contains more than one root {objectType}. Only the first one will be used.");
                    break;
            }

            return assets[0];
        }

        private async UniTask<AssetBundleData> WaitForDependencyAsync(
            SceneAssetBundleManifest? manifest,
            string hash, URLSubdirectory customEmbeddedSubdirectory,
            IPartitionComponent partition, CancellationToken ct)
        {
            // Inherit partition from the parent promise
            // we don't know the type of the dependency
            var assetBundlePromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, GetAssetBundleIntention.FromHash(null, hash, manifest: manifest, customEmbeddedSubDirectory: customEmbeddedSubdirectory), partition);

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

        private static TextAsset? GetMetadata(AssetBundle assetBundle) =>
            assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME);
    }
}
