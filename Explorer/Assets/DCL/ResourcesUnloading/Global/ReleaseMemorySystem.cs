using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Abstract;
using ECS.Groups;
using ECS.SceneLifeCycle.IncreasingRadius;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly UnloadStrategyHandler unloadStrategyHandler;
        private readonly SceneLoadingLimit sceneLoadingLimit;

        internal ReleaseMemorySystem(Arch.Core.World world, IMemoryUsageProvider memoryBudgetProvider,
            UnloadStrategyHandler unloadStrategyHandler, SceneLoadingLimit sceneLoadingLimit) : base(world)
        {
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.unloadStrategyHandler = unloadStrategyHandler;
            this.sceneLoadingLimit = sceneLoadingLimit;
        }

        protected override void Update(float t)
        {
            sceneLoadingLimit.ReportMemoryState(memoryBudgetProvider.IsMemoryNormal(), memoryBudgetProvider.IsInAbundance());
            unloadStrategyHandler.ReportMemoryState(memoryBudgetProvider.IsMemoryNormal(), memoryBudgetProvider.IsInAbundance());
        }
    }
}
