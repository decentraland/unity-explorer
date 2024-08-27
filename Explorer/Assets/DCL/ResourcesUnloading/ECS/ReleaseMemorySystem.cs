using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly ICacheCleaner cacheCleaner;

        internal ReleaseMemorySystem(Arch.Core.World world, ICacheCleaner cacheCleaner, IMemoryUsageProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
                cacheCleaner.UnloadCache();

            cacheCleaner.UpdateProfilingCounters();
        }
    }
}
