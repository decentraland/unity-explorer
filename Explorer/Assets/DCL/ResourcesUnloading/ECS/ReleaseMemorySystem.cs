using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly ICacheCleaner cacheCleaner;

        private readonly IUnloadStrategy unloadStrategy;

        internal ReleaseMemorySystem(Arch.Core.World world, ICacheCleaner cacheCleaner, IMemoryUsageProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
            unloadStrategy = new StandardUnloadStrategy();
        }

        protected override void Update(float t)
        {
            unloadStrategy.TryUnload(memoryBudgetProvider, cacheCleaner);
            cacheCleaner.UpdateProfilingCounters();
        }
    }
}
