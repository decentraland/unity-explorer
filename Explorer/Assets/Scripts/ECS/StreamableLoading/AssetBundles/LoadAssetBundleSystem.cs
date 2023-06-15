using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadAssetBundleSystem : LoadSystemBase<AssetBundleData, GetAssetBundleIntention>
    {
        // Parsing executes on the main thread so we need only one instance at a time
        private static readonly AssetBundleMetadata REUSABLE_METADATA = new ();

        private const string METADATA_FILENAME = "metadata.json";
        private const string METRICS_FILENAME = "metrics.json";

        internal LoadAssetBundleSystem(World world, IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache) : base(world, cache) { }

        protected override async UniTask<StreamableLoadingResult<AssetBundleData>> FlowInternal(GetAssetBundleIntention intention, CancellationToken ct)
        {
            UnityWebRequest webRequest = intention.cacheHash.HasValue
                ? UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL, intention.cacheHash.Value)
                : UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL);

            await webRequest.SendWebRequest().WithCancellation(ct);
            AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest);

            // resolve dependencies
            string metadata = GetMetadata(assetBundle)?.text;

            // get metrics
            TextAsset metricsFile = assetBundle.LoadAsset<TextAsset>(METRICS_FILENAME);

            // Switch to thread pool to parse JSONs

            await UniTask.SwitchToThreadPool();

            AssetBundleMetrics? metrics = metricsFile != null ? JsonUtility.FromJson<AssetBundleMetrics>(metricsFile.text) : null;

            if (metadata != null)
            {
                // Parse metadata
                JsonUtility.FromJsonOverwrite(metadata, REUSABLE_METADATA);

                // Construct dependency promises and wait for them
                // Switch to main thread to create dependency promises
                await UniTask.SwitchToMainThread();

                // WhenAll uses pool under the hood
                await UniTask.WhenAll(REUSABLE_METADATA.dependencies.Select(hash => WaitForDependency(hash, ct)));
            }

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

        private async UniTask WaitForDependency(string hash, CancellationToken ct)
        {
            var assetBundlePromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>.Create(World, new GetAssetBundleIntention(hash));

            try { await assetBundlePromise.ToUniTask(World, cancellationToken: ct); }
            catch (OperationCanceledException) { assetBundlePromise.ForgetLoading(World); }
        }

        private static TextAsset GetMetadata(AssetBundle assetBundle) =>
            assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME);
    }
}
