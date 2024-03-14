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
using System.Collections.Generic;
using System.Linq;
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

        private async UniTask<AssetBundleData[]> LoadDependenciesAsync(SceneAssetBundleManifest manifest, IPartitionComponent partition, URLSubdirectory customEmbeddedSubdirectory, AssetBundle assetBundle, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            string metadata;

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

                AssetBundleData[] dependencies = await LoadDependenciesAsync(intention.Manifest, partition, intention.CommonArguments.CustomEmbeddedSubDirectory, assetBundle, ct);

                await UniTask.SwitchToMainThread();
                ct.ThrowIfCancellationRequested();

                GameObject? mainGameObject = await LoadAllAssetsAsync<GameObject>(assetBundle, ct);
                if(mainGameObject!=null)
                    return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, mainGameObject, dependencies));

                Texture? mainTexture = await LoadAllAssetsAsync<Texture>(assetBundle, ct);
                return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, mainTexture, dependencies));
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

        private async UniTask<T?> LoadAllAssetsAsync<T>(AssetBundle assetBundle, CancellationToken ct) where T : Object {
            using AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct);

            // we are only interested in game objects
            AssetBundleRequest asyncOp = assetBundle.LoadAllAssetsAsync<T>();
            await asyncOp.WithCancellation(ct);

            // Can't avoid an array instantiation - no API with List
            // Can't avoid casting - no generic API
            var asset = new List<T?>(asyncOp.allAssets.Cast<T>());

            if (asset.Count > 1)
                ReportHub.LogError(GetReportCategory(), $"AssetBundle {assetBundle.name} contains more than one root GameObject. Only the first one will be used.");

            T? rootAsset = asset.Count > 0 ? asset[0] : null;

            return rootAsset;
        }

        private async UniTask<AssetBundleData> WaitForDependencyAsync(SceneAssetBundleManifest manifest,
            string hash, URLSubdirectory customEmbeddedSubdirectory,
            IPartitionComponent partition, CancellationToken ct)
        {
            // Inherit partition from the parent promise
            var assetBundlePromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, GetAssetBundleIntention.FromHash(hash, manifest: manifest, customEmbeddedSubDirectory: customEmbeddedSubdirectory), partition);

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

        private static TextAsset GetMetadata(AssetBundle assetBundle) =>
            assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME);
    }
}
