using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
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

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadWearableAssetBundleSystem : LoadSystemBase<AssetBundleData, GetWearableAssetBundleIntention>
    {
        private const string METADATA_FILENAME = "metadata.json";
        private const string METRICS_FILENAME = "metrics.json";
        private static readonly ThreadSafeObjectPool<AssetBundleMetadata> METADATA_POOL
            = new (() => new AssetBundleMetadata(), maxSize: 100);

        private readonly AssetBundleLoadingMutex loadingMutex;

        internal LoadWearableAssetBundleSystem(World world,
            IStreamableCache<AssetBundleData, GetWearableAssetBundleIntention> cache,
            MutexSync mutexSync,
            AssetBundleLoadingMutex loadingMutex) : base(world, cache, mutexSync)
        {
            this.loadingMutex = loadingMutex;
        }

        private async UniTask LoadDependencies(GetWearableAssetBundleIntention wearableAssetBundleIntention, IPartitionComponent partition, AssetBundle assetBundle, CancellationToken ct)
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
                await UniTask.WhenAll(reusableMetadata.Value.dependencies.Select(hash => WaitForDependency(wearableAssetBundleIntention, hash, partition, ct)));
            }
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> FlowInternal(GetWearableAssetBundleIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
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

            await LoadDependencies(intention, partition, assetBundle, ct);

            await UniTask.SwitchToMainThread();
            ct.ThrowIfCancellationRequested();

            GameObject gameObjects = await LoadAllAssets(assetBundle, ct);

            return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, gameObjects));
        }

        private async UniTask<GameObject> LoadAllAssets(AssetBundle assetBundle, CancellationToken ct)
        {
            using AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.Acquire(ct);

            // we are only interested in game objects
            AssetBundleRequest asyncOp = assetBundle.LoadAllAssetsAsync<GameObject>();
            await asyncOp.WithCancellation(ct);

            // Can't avoid an array instantiation - no API with List
            // Can't avoid casting - no generic API
            var gameObjects = new List<GameObject>(asyncOp.allAssets.Cast<GameObject>());

            if (gameObjects.Count > 1)
                ReportHub.LogError(GetReportCategory(), $"AssetBundle {assetBundle.name} contains more than one root gameobject. Only the first one will be used.");

            GameObject rootGameObject = gameObjects.Count > 0 ? gameObjects[0] : null;

            return rootGameObject;
        }

        private async UniTask WaitForDependency(GetWearableAssetBundleIntention assetBundleIntention, string hash, IPartitionComponent partition, CancellationToken ct)
        {
            //TODO: Remove this hack by fixing the asset bundle converter
            hash = assetBundleIntention.WearableAssetBundleManifest.GetCorrectCapsLock(hash);

            // Inherit partition from the parent promise
            var assetBundlePromise = AssetPromise<AssetBundleData, GetWearableAssetBundleIntention>.Create(World, GetWearableAssetBundleIntention.FromHash(assetBundleIntention.WearableAssetBundleManifest, hash, assetBundleIntention.BodyShape), partition);

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
