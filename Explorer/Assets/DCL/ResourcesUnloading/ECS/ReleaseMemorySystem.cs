using System;
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
        private readonly UnloadStrategy unloadStrategy;
        private readonly ICacheCleaner cacheCleaner;

        internal ReleaseMemorySystem(Arch.Core.World world, IMemoryUsageProvider memoryBudgetProvider,
            UnloadStrategy unloadStrategy, ICacheCleaner cacheCleaner) : base(world)
        {
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.unloadStrategy = unloadStrategy;
            this.cacheCleaner = cacheCleaner;
        }

        protected override void Update(float t)
        {
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
                unloadStrategy.TryUnload(cacheCleaner);
            else
                unloadStrategy.Reset();
        }
    }
}
