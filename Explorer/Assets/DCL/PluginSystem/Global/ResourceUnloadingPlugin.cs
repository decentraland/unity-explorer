using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;

namespace DCL.PluginSystem.Global
{
    public class ResourceUnloadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly MemoryBudget memoryBudget;
        private readonly CacheCleaner cacheCleaner;

        public ResourceUnloadingPlugin(MemoryBudget memoryBudget, CacheCleaner cacheCleaner)
        {
            this.memoryBudget = memoryBudget;
            this.cacheCleaner = cacheCleaner;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, cacheCleaner, memoryBudget);
        }
    }
}
