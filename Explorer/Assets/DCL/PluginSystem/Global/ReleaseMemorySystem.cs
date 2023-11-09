using Arch.SystemGroups;
using DCL.PerformanceBudgeting;
using DCL.ResourceUnloading;
using ECS.Abstract;
using ECS.Groups;

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

            cacheCleaner.UpdateProfilingCounters();
        }
    }
}
