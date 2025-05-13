using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.SceneLifeCycle.IncreasingRadius;

namespace DCL.PluginSystem.Global
{
    public class ResourceUnloadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IMemoryUsageProvider memoryBudget;
        private readonly UnloadStrategyHandler unloadStrategyHandler;
        private readonly SceneLoadingLimit sceneLoadingLimit;

        public ResourceUnloadingPlugin(IMemoryUsageProvider memoryBudget, CacheCleaner cacheCleaner, SceneLoadingLimit sceneLoadingLimit)
        {
            this.memoryBudget = memoryBudget;
            this.sceneLoadingLimit = sceneLoadingLimit;

            unloadStrategyHandler =
                new UnloadStrategyHandler(cacheCleaner);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ReleaseMemorySystem.InjectToWorld(ref builder, memoryBudget, unloadStrategyHandler, sceneLoadingLimit);
        }
    }
}
