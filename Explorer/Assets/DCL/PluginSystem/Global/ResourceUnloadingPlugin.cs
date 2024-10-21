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
        private readonly UnloadStrategy unloadStrategy;
        private readonly ICacheCleaner cacheCleaner;


        public ResourceUnloadingPlugin(MemoryBudget memoryBudget, ICacheCleaner cacheCleaner,
            IRealmPartitionSettings realmPartitionSettings)
        {
            this.memoryBudget = memoryBudget;
            this.cacheCleaner = cacheCleaner;
            //Outer strategy is more aggresive and runs last
            unloadStrategy = new UnloadUnusedAssetUnloadStrategy(
                new ReduceLoadingRadiusUnloadStrategy(new StandardUnloadStrategy(), realmPartitionSettings));
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, memoryBudget, unloadStrategy, cacheCleaner);
        }
    }
}
