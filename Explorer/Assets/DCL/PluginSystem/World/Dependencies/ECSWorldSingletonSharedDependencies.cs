using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.PerformanceBudgeting;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly ISystemGroupAggregate<IPartitionComponent>.IFactory AggregateFactory;
        public readonly ISceneEntityFactory EntityFactory;
        public readonly ConcurrentLoadingBudgetProvider LoadingBudgetProvider;
        public readonly IConcurrentBudgetProvider FrameTimeBudgetProvider;
        public readonly MemoryBudgetProvider MemoryBudgetProvider;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry,
            IReportsHandlingSettings reportsHandlingSettings,
            ISceneEntityFactory entityFactory,
            ISystemGroupAggregate<IPartitionComponent>.IFactory aggregateFactory,
            ConcurrentLoadingBudgetProvider loadingBudgetProvider,
            IConcurrentBudgetProvider frameTimeBudgetProvider,
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
