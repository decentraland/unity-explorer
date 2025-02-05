using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.DeferredLoading;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly UnloadStrategyHandler unloadStrategyHandler;
        private readonly QualityReductorManager qualityReductorManager;

        internal ReleaseMemorySystem(Arch.Core.World world, IMemoryUsageProvider memoryBudgetProvider,
            UnloadStrategyHandler unloadStrategyHandler) : base(world)
        {
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.unloadStrategyHandler = unloadStrategyHandler;
            qualityReductorManager = new QualityReductorManager(world);
        }

        protected override void Update(float t)
        {
            if (memoryBudgetProvider.GetMemoryUsageStatus() == MemoryUsageStatus.FULL)
                qualityReductorManager.RequestQualityReduction(World);
            else
                qualityReductorManager.RequestQualityIncrease(World);
            
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
                unloadStrategyHandler.TryUnload();
            else
                unloadStrategyHandler.ResetToNormal();
        }
    }
}
