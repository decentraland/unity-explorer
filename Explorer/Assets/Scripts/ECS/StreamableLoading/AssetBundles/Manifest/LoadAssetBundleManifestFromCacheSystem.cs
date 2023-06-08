using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateBefore(typeof(StartLoadingAssetBundleManifestSystem))]
    public partial class LoadAssetBundleManifestFromCacheSystem : LoadFromCacheSystemBase<GetAssetBundleManifestIntention, SceneAssetBundleManifest>
    {
        internal LoadAssetBundleManifestFromCacheSystem(World world, IStreamableCache<SceneAssetBundleManifest, GetAssetBundleManifestIntention> cache) : base(world, cache) { }
    }
}
