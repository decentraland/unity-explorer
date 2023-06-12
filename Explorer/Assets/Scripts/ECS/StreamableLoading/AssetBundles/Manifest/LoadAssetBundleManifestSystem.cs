using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadAssetBundleManifestSystem : LoadSystemBase<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private readonly string assetBundleURL;

        public LoadAssetBundleManifestSystem(World world, IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention> cache, string assetBundleURL)
            : base(world, cache)
        {
            this.assetBundleURL = assetBundleURL;
        }

        protected override async UniTask<StreamableLoadingResult<SceneAssetBundleManifest>> FlowInternal(GetAssetBundleManifestIntention intention, CancellationToken ct)
        {
            var wr = UnityWebRequest.Get(intention.CommonArguments.URL);
            await wr.SendWebRequest().WithCancellation(ct);

            string text = wr.downloadHandler.text;

            // Parse off the main thread
            await UniTask.SwitchToThreadPool();

            return new StreamableLoadingResult<SceneAssetBundleManifest>(
                new SceneAssetBundleManifest(assetBundleURL, JsonUtility.FromJson<SceneAbDto>(text)));
        }
    }
}
