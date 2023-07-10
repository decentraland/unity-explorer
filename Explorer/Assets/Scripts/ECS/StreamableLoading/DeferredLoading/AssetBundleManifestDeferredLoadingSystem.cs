using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Prioritization.DeferredLoading;
using ECS.StreamableLoading.AssetBundles.Manifest;
using SceneRunner.Scene;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareAssetBundleManifestParametersSystem))]
    [UpdateBefore(typeof(LoadAssetBundleManifestSystem))]
    public partial class AssetBundleManifestDeferredLoadingSystem : DeferredLoadingSystem<SceneAssetBundleManifest, GetAssetBundleManifestIntention>
    {
        public AssetBundleManifestDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world, concurrentLoadingBudgetProvider) { }
    }
}
