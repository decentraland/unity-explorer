using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;

namespace DCL.PluginSystem.Global
{
    public class ResourceUnloadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly MemoryBudget memoryBudget;
        private readonly CacheCleaner cacheCleaner;
        private readonly IUnloadStrategy[] unloadStrategies;
        private const int FRAME_FAIL_THRESHOLD = 60;

        public ResourceUnloadingPlugin(MemoryBudget memoryBudget, CacheCleaner cacheCleaner)
        {
            this.memoryBudget = memoryBudget;
            this.cacheCleaner = cacheCleaner;
            unloadStrategies = new IUnloadStrategy[]
            {
                new StandardUnloadStrategy(),
                new AggressiveUnloadStrategy()
            };
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, cacheCleaner, memoryBudget, unloadStrategies,
                FRAME_FAIL_THRESHOLD);
        }
    }
}
