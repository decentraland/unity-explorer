using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Systems;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Finish Asset Bundle Loading
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(StartLoadingAssetBundleSystem))]
    public partial class ConcludeAssetBundleLoadingSystem : ConcludeLoadingSystemBase<AssetBundle, GetAssetBundleIntention>
    {
        /// <summary>
        ///     Explicit cache is never required as AssetBundles are cached by Unity's Caching system
        /// </summary>
        internal ConcludeAssetBundleLoadingSystem(World world) : base(world, NoCache<AssetBundle, GetAssetBundleIntention>.INSTANCE) { }

        protected override AssetBundle GetAsset(UnityWebRequest webRequest, in GetAssetBundleIntention intention) =>
            DownloadHandlerAssetBundle.GetContent(webRequest);
    }
}
