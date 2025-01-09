using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Prioritization;

namespace DCL.PluginSystem.Global
{
    public class ResourceUnloadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IMemoryUsageProvider memoryBudget;
        private readonly UnloadStrategyHandler unloadStrategyHandler;


        public ResourceUnloadingPlugin(IMemoryUsageProvider memoryBudget, CacheCleaner cacheCleaner,
            IRealmPartitionSettings realmPartitionSettings)
        {
            this.memoryBudget = memoryBudget;
            unloadStrategyHandler =
                new UnloadStrategyHandler(realmPartitionSettings, cacheCleaner);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, memoryBudget, unloadStrategyHandler);
        }
    }
}
