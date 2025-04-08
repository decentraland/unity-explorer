using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class PartialLoadGlobalAssetBundleSystem : PartialLoadAssetBundleSystem
    {
        public PartialLoadGlobalAssetBundleSystem(World world, IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache, IWebRequestController webRequestController, AssetBundleLoadingMutex loadingMutex) : base(world, cache, webRequestController, loadingMutex) { }
    }
}
