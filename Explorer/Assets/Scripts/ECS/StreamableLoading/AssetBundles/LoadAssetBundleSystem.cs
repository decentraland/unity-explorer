using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;
using Utility.Pool;
using Utility.ThreadSafePool;

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

        private async UniTask LoadDependencies(SceneAssetBundleManifest manifest, IPartitionComponent partition, URLSubdirectory customEmbeddedSubdirectory, AssetBundle assetBundle, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            string metadata;

            using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.Acquire(ct))
                metadata = GetMetadata(assetBundle)?.text;

            if (metadata != null)
            {
                using PoolExtensions.Scope<AssetBundleMetadata> reusableMetadata = METADATA_POOL.AutoScope();

                // Parse metadata
                JsonUtility.FromJsonOverwrite(metadata, reusableMetadata.Value);

                // Construct dependency promises and wait for them
                // Switch to main thread to create dependency promises
                await UniTask.SwitchToMainThread();

                // WhenAll uses pool under the hood
                await UniTask.WhenAll(reusableMetadata.Value.dependencies.Select(hash => WaitForDependency(manifest, hash, customEmbeddedSubdirectory, partition, ct)));
            }
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> FlowInternal(GetAssetBundleIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            AssetBundle assetBundle;

            using (UnityWebRequest webRequest = intention.cacheHash.HasValue
                       ? UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL, intention.cacheHash.Value)
                       : UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL))
            {
                ((DownloadHandlerAssetBundle)webRequest.downloadHandler).autoLoadAssetBundle = false;
                await webRequest.SendWebRequest().WithCancellation(ct);

                using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.Acquire(ct))
                    assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest);

                // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
                acquiredBudget.Release();

                // if GetContent prints an error, null will be thrown
                if (assetBundle == null)
                    throw new NullReferenceException($"{intention.Hash} Asset Bundle is null: {webRequest.downloadHandler.error}");
            }

            // get metrics

            TextAsset metricsFile;

            using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.Acquire(ct))
                metricsFile = assetBundle.LoadAsset<TextAsset>(METRICS_FILENAME);

            // Switch to thread pool to parse JSONs

            await UniTask.SwitchToThreadPool();
            ct.ThrowIfCancellationRequested();

            AssetBundleMetrics? metrics = metricsFile != null ? JsonUtility.FromJson<AssetBundleMetrics>(metricsFile.text) : null;

            await LoadDependencies(intention.Manifest, partition, intention.CommonArguments.CustomEmbeddedSubDirectory, assetBundle, ct);

            await UniTask.SwitchToMainThread();
            ct.ThrowIfCancellationRequested();

            GameObject gameObjects = await LoadAllAssets(assetBundle, ct);
            Debug.Log($"VV:: loaded Asset bundle {gameObjects?.name}", gameObjects);

            return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, gameObjects));
        }

        private async UniTask<GameObject> LoadAllAssets(AssetBundle assetBundle, CancellationToken ct)
        {
            using AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.Acquire(ct);

            // we are only interested in game objects
            AssetBundleRequest asyncOp = assetBundle.LoadAllAssetsAsync<GameObject>();
            await asyncOp.WithCancellation(ct);

            if (asyncOp.allAssets.Length == 0)
                return null;

            if (asyncOp.allAssets.Length > 1)
                ReportHub.LogError(GetReportCategory(), $"AssetBundle {assetBundle.name} contains more than one root gameobject. Only the first one will be used.");

            return asyncOp.allAssets[0] as GameObject;
        }

        private async UniTask WaitForDependency(SceneAssetBundleManifest manifest,
            string hash, URLSubdirectory customEmbeddedSubdirectory,
            IPartitionComponent partition, CancellationToken ct)
        {
            // Inherit partition from the parent promise
            var assetBundlePromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, GetAssetBundleIntention.FromHash(hash, manifest: manifest, customEmbeddedSubDirectory: customEmbeddedSubdirectory), partition);

            try
            {
                assetBundlePromise = await assetBundlePromise.ToUniTask(World, cancellationToken: ct);

                if (!assetBundlePromise.TryGetResult(World, out StreamableLoadingResult<AssetBundleData> depResult))
                    throw new Exception($"Dependency {hash} is not resolved");

                if (!depResult.Succeeded)
                    throw new Exception($"Dependency {hash} resolution failed", depResult.Exception);
            }
            catch (OperationCanceledException) { assetBundlePromise.ForgetLoading(World); }
        }

        private static TextAsset GetMetadata(AssetBundle assetBundle) =>
            assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME);
    }
}
