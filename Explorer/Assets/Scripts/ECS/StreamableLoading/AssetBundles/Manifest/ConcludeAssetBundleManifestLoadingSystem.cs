using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    [UpdateAfter(typeof(StartLoadingAssetBundleManifestSystem))]
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class ConcludeAssetBundleManifestLoadingSystem : ConcludeLoadingSystemBase<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        private readonly string assetBundleURL;

        internal ConcludeAssetBundleManifestLoadingSystem(World world, IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention> cache, string assetBundleURL)
            : base(world, cache)
        {
            this.assetBundleURL = assetBundleURL;
        }

        protected override SceneAssetBundleManifest GetAsset(UnityWebRequest webRequest, in GetAssetBundleManifestIntention intention) =>
            new (assetBundleURL, JsonUtility.FromJson<SceneAbDto>(webRequest.downloadHandler.text));
    }
}
