using Arch.SystemGroups;
using DCL.CacheCleanUp;
using DCL.PerformanceBudgeting;
using DCL.Profiling;
using ECS.Abstract;
using ECS.Groups;
using System.Linq;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly MemoryBudgetProvider memoryBudgetProvider;
        private readonly CacheCleaner cacheCleaner;

        private ReleaseMemorySystem(Arch.Core.World world, CacheCleaner cacheCleaner, MemoryBudgetProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.Normal)
                cacheCleaner.UnloadCache();

            ProfilingCounters.WearablesAssetsInCatalogAmount.Value = cacheCleaner.WearableCatalog.WearableDictionary.Values
                                                                                 .Where(wearable => wearable.WearableAssets != null)
                                                                                 .Sum(wearable => wearable.WearableAssets.Count(result => result is { Asset: not null }));

            ProfilingCounters.WearablesAssetsInCacheAmount.Value = cacheCleaner.WearableAssetsCache.Cache.Count;
        }
    }
}
