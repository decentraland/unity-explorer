using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Prioritization;

namespace DCL.PluginSystem.Global
{
    public class ResourceUnloadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly MemoryBudget memoryBudget;
        private readonly UnloadStrategyHandler unloadStrategyHandler;
        private const int FRAME_FAIL_THRESHOLD = 60;


        public ResourceUnloadingPlugin(MemoryBudget memoryBudget, CacheCleaner cacheCleaner,
            IRealmPartitionSettings realmPartitionSettings)
        {
            this.memoryBudget = memoryBudget;
            unloadStrategyHandler =
                new UnloadStrategyHandler(realmPartitionSettings, FRAME_FAIL_THRESHOLD, cacheCleaner);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, memoryBudget, unloadStrategyHandler);
        }
    }
}
