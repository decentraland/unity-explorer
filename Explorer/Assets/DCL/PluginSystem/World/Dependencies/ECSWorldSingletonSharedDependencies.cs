using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using ECS.Prioritization.Components;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly ISystemGroupAggregate<IPartitionComponent>.IFactory AggregateFactory;
        public readonly ISceneEntityFactory EntityFactory;
        public readonly IConcurrentBudgetProvider LoadingBudgetProvider;
        public readonly FrameTimeCapBudgetProvider FrameTimeBudgetProvider;
        public readonly MemoryBudgetProvider MemoryBudgetProvider;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry,
            IReportsHandlingSettings reportsHandlingSettings,
            ISceneEntityFactory entityFactory,
            ISystemGroupAggregate<IPartitionComponent>.IFactory aggregateFactory,
            IConcurrentBudgetProvider loadingBudgetProvider,
            FrameTimeCapBudgetProvider frameTimeBudgetProvider,
            MemoryBudgetProvider memoryBudgetProvider)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;

            EntityFactory = entityFactory;
            AggregateFactory = aggregateFactory;

            LoadingBudgetProvider = loadingBudgetProvider;
            FrameTimeBudgetProvider = frameTimeBudgetProvider;
            MemoryBudgetProvider = memoryBudgetProvider;
        }
    }
}
