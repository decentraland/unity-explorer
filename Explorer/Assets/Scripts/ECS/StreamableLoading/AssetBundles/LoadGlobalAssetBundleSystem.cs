using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    public partial class LoadGlobalAssetBundleSystem : LoadAssetBundleSystem
    {
        internal LoadGlobalAssetBundleSystem(World world, MemoryBudgetProvider memoryBudgetProvider, IStreamableCache<AssetBundleData, GetAssetBundleIntention> cache, MutexSync mutexSync, AssetBundleLoadingMutex loadingMutex) :
            base(world, memoryBudgetProvider, cache, mutexSync, loadingMutex) { }
    }
}
