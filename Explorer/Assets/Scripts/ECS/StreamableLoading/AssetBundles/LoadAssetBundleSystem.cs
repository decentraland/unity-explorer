using Arch.Core;
using Arch.SystemGroups;
using AssetManagement;
using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly ThreadSafeObjectPool<AssetBundleMetadata> METADATA_POOL
            = new (() => new AssetBundleMetadata(), maxSize: 100);

        private const string METADATA_FILENAME = "metadata.json";
        private const string METRICS_FILENAME = "metrics.json";

        private readonly AssetBundleManifest assetBundleManifest;

        internal LoadAssetBundleSystem(World world, IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache,
            AssetBundleManifest assetBundleManifest, MutexSync mutexSync, IConcurrentBudgetProvider loadingBudgetProvider = null) : base(world, cache, mutexSync, loadingBudgetProvider)
        {
            this.assetBundleManifest = assetBundleManifest;
        }

        private async UniTask LoadDependencies(GetAssetBundleIntention intention, IPartitionComponent partition, AssetBundle assetBundle, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

#region KILL_ME
            // HACK! Load Asset Bundle Manifest from streaming assets
            if (intention.CommonArguments.CurrentSource == AssetSource.EMBEDDED)
            {
                string[] dependencies = assetBundleManifest?.GetAllDependencies(intention.Hash) ?? Array.Empty<string>();
                await UniTask.WhenAll(dependencies.Select(hash => WaitForDependency(hash, partition, ct)));
                return;
            }
#endregion

            // resolve dependencies
            string metadata = GetMetadata(assetBundle)?.text;

            if (metadata != null)
            {
                using PoolExtensions.Scope<AssetBundleMetadata> reusableMetadata = METADATA_POOL.AutoScope();

                // Parse metadata
                JsonUtility.FromJsonOverwrite(metadata, reusableMetadata.Value);

                // Construct dependency promises and wait for them
                // Switch to main thread to create dependency promises
                await UniTask.SwitchToMainThread();

                // WhenAll uses pool under the hood
                await UniTask.WhenAll(reusableMetadata.Value.dependencies.Select(hash => WaitForDependency(hash, partition, ct)));
            }
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> FlowInternal(GetAssetBundleIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            UnityWebRequest webRequest = intention.cacheHash.HasValue
                ? UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL, intention.cacheHash.Value)
                : UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL);

            await webRequest.SendWebRequest().WithCancellation(ct);
            AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest);

            // if GetContent prints an error, null will be thrown
            if (assetBundle == null)
                throw new NullReferenceException($"{intention.Hash} Asset Bundle is null");

            // get metrics
            TextAsset metricsFile = assetBundle.LoadAsset<TextAsset>(METRICS_FILENAME);

            // Switch to thread pool to parse JSONs

            await UniTask.SwitchToThreadPool();

            AssetBundleMetrics? metrics = metricsFile != null ? JsonUtility.FromJson<AssetBundleMetrics>(metricsFile.text) : null;

            await LoadDependencies(intention, partition, assetBundle, ct);

            await UniTask.SwitchToMainThread();
            IReadOnlyList<GameObject> gameObjects = await LoadAllAssets(assetBundle, ct);

            return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, gameObjects));
        }

        private async UniTask<IReadOnlyList<GameObject>> LoadAllAssets(AssetBundle assetBundle, CancellationToken ct)
        {
            // we are only interested in game objects
            AssetBundleRequest asyncOp = assetBundle.LoadAllAssetsAsync<GameObject>();
            await asyncOp.WithCancellation(ct);

            // Can't avoid an array instantiation - no API with List
            // Can't avoid casting - no generic API
            return asyncOp.allAssets.Length > 0 ? new List<GameObject>(asyncOp.allAssets.Cast<GameObject>()) : Array.Empty<GameObject>();
        }

        private async UniTask WaitForDependency(string hash, IPartitionComponent partition, CancellationToken ct)
        {
            // Inherit partition from the parent promise
            var assetBundlePromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, GetAssetBundleIntention.FromHash(hash), partition);

            try
            {
                AssetPromise<AssetBundleData, GetAssetBundleIntention> depPromise = await assetBundlePromise.ToUniTask(World, cancellationToken: ct);

                if (!depPromise.TryGetResult(World, out StreamableLoadingResult<AssetBundleData> depResult))
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
