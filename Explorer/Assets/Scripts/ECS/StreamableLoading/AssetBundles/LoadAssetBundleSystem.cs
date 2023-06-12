using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadAssetBundleSystem : LoadSystemBase<AssetBundle, GetAssetBundleIntention>
    {
        // Parsing executes on the main thread so we need only one instance at a time
        private static readonly AssetBundleMetadata REUSABLE_METADATA = new ();

        private const string METADATA_FILENAME = "metadata.json";

        internal LoadAssetBundleSystem(World world, IStreamableCache<AssetBundle, GetAssetBundleIntention> cache) : base(world, cache) { }

        protected override async UniTask<StreamableLoadingResult<AssetBundle>> FlowInternal(GetAssetBundleIntention intention, CancellationToken ct)
        {
            UnityWebRequest webRequest = intention.cacheHash.HasValue
                ? UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL, intention.cacheHash.Value)
                : UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL);

            await webRequest.SendWebRequest().WithCancellation(ct);
            AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(webRequest);

            // TODO resolve dependencies

            TextAsset metadata = GetMetadata(assetBundle);

            if (metadata != null)
            {
                // Parse metadata
                JsonUtility.FromJsonOverwrite(metadata.text, REUSABLE_METADATA);

                // Construct dependency promises and wait for them

                // WhenAll uses pool under the hood
                await UniTask.WhenAll(REUSABLE_METADATA.dependencies.Select(hash => WaitForDependency(hash, ct)));
            }

            return new StreamableLoadingResult<AssetBundle>(assetBundle);
        }

        private async UniTask WaitForDependency(string hash, CancellationToken ct)
        {
            var assetBundlePromise = AssetPromise<AssetBundle, GetAssetBundleIntention>.Create(World, new GetAssetBundleIntention(hash));

            try { await assetBundlePromise.ToUniTask(World, cancellationToken: ct); }
            catch (OperationCanceledException) { assetBundlePromise.ForgetLoading(World); }
        }

        private static TextAsset GetMetadata(AssetBundle assetBundle) =>
            assetBundle.LoadAsset<TextAsset>(METADATA_FILENAME);
    }
}
