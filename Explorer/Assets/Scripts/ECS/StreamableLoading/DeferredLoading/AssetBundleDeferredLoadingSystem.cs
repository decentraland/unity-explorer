using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Prioritization.DeferredLoading;
using ECS.StreamableLoading.AssetBundles;

namespace ECS.StreamableLoading.DeferredLoading
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadAssetBundleSystem))]
    public partial class AssetBundleDeferredLoadingSystem : DeferredLoadingSystem<AssetBundleData, GetAssetBundleIntention>
    {
        public AssetBundleDeferredLoadingSystem(World world, IConcurrentBudgetProvider concurrentLoadingBudgetProvider) : base(world, concurrentLoadingBudgetProvider) { }
    }
}
