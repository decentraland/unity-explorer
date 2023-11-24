using Arch.SystemGroups;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;

namespace DCL.PluginSystem.Global
{
    public class ResourceUnloadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly MemoryBudgetProvider memoryBudgetProvider;
        private readonly CacheCleaner cacheCleaner;

        public ResourceUnloadingPlugin(MemoryBudgetProvider memoryBudgetProvider, CacheCleaner cacheCleaner)
        {
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.cacheCleaner = cacheCleaner;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, cacheCleaner, memoryBudgetProvider);
        }
    }
}
