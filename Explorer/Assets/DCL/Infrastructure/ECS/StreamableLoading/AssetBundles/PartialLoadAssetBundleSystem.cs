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
using AssetManagement;
using DCL.WebRequests;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PartialLoadAssetBundleSystem : PartialDownloadSystemBase<AssetBundleData, GetAssetBundleIntention>
    {
        private const string METADATA_FILENAME = "metadata.json";
        private const string METRICS_FILENAME = "metrics.json";

        private static readonly ThreadSafeObjectPool<AssetBundleMetadata> METADATA_POOL
            = new (() => new AssetBundleMetadata(),
                actionOnRelease: metadata => metadata.Clear()
              , maxSize: 100);

        private readonly AssetBundleLoadingMutex loadingMutex;

        internal PartialLoadAssetBundleSystem(World world,
            IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache,
            IWebRequestController webRequestController,
            AssetBundleLoadingMutex loadingMutex) : base(world, cache, webRequestController)
        {
            this.loadingMutex = loadingMutex;
        }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> ProcessCompletedDataAsync(StreamableLoadingState state, GetAssetBundleIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            PartialDownloadStream stream = state.ClaimOwnershipOverFullyDownloadedData();
            AssetBundle? assetBundle;

            await UniTask.SwitchToMainThread();

            try
            {
                assetBundle = await AssetBundle.LoadFromStreamAsync(stream);
            }
            catch (Exception e)
            {
                stream.Dispose();
                throw new Exception($"Exception occured on loading AssetBundle {intention.Hash} from stream", e);
            }

            try
            {
                // Release budget now to not hold it until dependencies are resolved to prevent a deadlock
                state.AcquiredBudget!.Release();

                if (!assetBundle)
                    throw new NullReferenceException($"{intention.Hash} Asset Bundle is null");

                // get metrics
                string? metricsJSON;
                string? metadataJSON;

                using (AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct))
                {
                    metricsJSON = assetBundle.LoadAsset<TextAsset>(METRICS_FILENAME)?.text;
                    metadataJSON = assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME)?.text;
                }

                // Switch to thread pool to parse JSONs
                await UniTask.SwitchToThreadPool();
                ct.ThrowIfCancellationRequested();

                AssetBundleMetrics? metrics = !string.IsNullOrEmpty(metricsJSON) ? JsonUtility.FromJson<AssetBundleMetrics>(metricsJSON) : null;
                AssetBundleData[] dependencies;
                var mainAsset = "";

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

                string version = intention.Manifest != null ? intention.Manifest.GetVersion() : string.Empty;
                string source = intention.CommonArguments.CurrentSource.ToStringNonAlloc();

                StreamableLoadingResult<AssetBundleData> result = await CreateAssetBundleDataAsync(assetBundle, metrics, intention.ExpectedObjectType, mainAsset, loadingMutex, dependencies, stream, GetReportData(), version, source, intention.LookForShaderAssets, ct);
                return result;
            }
            catch (Exception)
            {
                // If the loading process didn't finish successfully unload the bundle
                await UniTask.SwitchToMainThread();

                if (assetBundle)
                    await assetBundle.UnloadAsync(true);

                await stream.DisposeAsync();
                throw;
            }
        }

        private async UniTask<AssetBundleData[]> LoadDependenciesAsync(GetAssetBundleIntention parentIntent, IPartitionComponent partition, AssetBundleMetadata assetBundleMetadata, CancellationToken ct)
        {
            // Construct dependency promises and wait for them
            // Switch to main thread to create dependency promises
            await UniTask.SwitchToMainThread();

            SceneAssetBundleManifest? manifest = parentIntent.Manifest;
            URLSubdirectory customEmbeddedSubdirectory = parentIntent.CommonArguments.CustomEmbeddedSubDirectory;

            return await UniTask.WhenAll(assetBundleMetadata.dependencies.Select(hash => WaitForDependencyAsync(manifest, hash, customEmbeddedSubdirectory, partition, ct)));
        }

        internal static async UniTask<StreamableLoadingResult<AssetBundleData>> CreateAssetBundleDataAsync(
            AssetBundle assetBundle, AssetBundleMetrics? metrics, Type? expectedObjType, string? mainAsset,
            AssetBundleLoadingMutex loadingMutex,
            AssetBundleData[] dependencies,
            Stream stream,
            ReportData reportCategory,
            string version,
            string source,
            bool lookForShaderAssets,
            CancellationToken ct)
        {
            // if the type was not specified don't load any assets (we don't know when they will be indirectly requested)
            if (expectedObjType == null)
                return new StreamableLoadingResult<AssetBundleData>(new AssetBundleData(assetBundle, metrics, dependencies, stream));

            if (lookForShaderAssets && expectedObjType == typeof(GameObject))
            {
                //If there are no dependencies, it means that this gameobject asset bundle has the shader in it.
                //All gameobject asset bundles should at least have the dependency on the shader.
                //This will cause a material leak, as the same material will be loaded again. This needs to be solved at asset bundle level
                if (dependencies.Length == 0)
                    throw new StreamableLoadingException(LogType.Warning, nameof(PartialLoadAssetBundleSystem), new AssetBundleContainsShaderException(assetBundle.name));
            }

            Object? asset = await LoadAllAssetsAsync(assetBundle, expectedObjType, mainAsset, loadingMutex, reportCategory, ct);

            var assetBundleData = new AssetBundleData(assetBundle, metrics, asset, expectedObjType, dependencies, version, source, stream);

            // After this point it's no longer possible to load other assets from the asset bundle

            return new StreamableLoadingResult<AssetBundleData>(assetBundleData);
        }

        private static async UniTask<Object> LoadAllAssetsAsync(AssetBundle assetBundle, Type objectType, string? mainAsset, AssetBundleLoadingMutex loadingMutex, ReportData reportCategory,
            CancellationToken ct)
        {
            using AssetBundleLoadingMutex.LoadingRegion _ = await loadingMutex.AcquireAsync(ct);

            AssetBundleRequest? asyncOp = !string.IsNullOrEmpty(mainAsset)
                ? assetBundle.LoadAssetAsync(mainAsset)
                : assetBundle.LoadAllAssetsAsync(objectType);

            await asyncOp.WithCancellation(ct);

            Object[]? assets = asyncOp.allAssets;

            switch (assets.Length)
            {
                case 0:
                    throw new StreamableLoadingException(LogType.Warning, nameof(PartialLoadAssetBundleSystem), new AssetBundleMissingMainAssetException(assetBundle.name, objectType));
                case > 1:
                    ReportHub.LogError(reportCategory, $"AssetBundle {assetBundle.name} contains more than one root {objectType}. Only the first one will be used.");
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
    }
}
