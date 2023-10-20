using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using Diagnostics.ReportsHandling;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly ISystemGroupAggregate<IPartitionComponent>.IFactory AggregateFactory;
        public readonly ISceneEntityFactory EntityFactory;
        public readonly IConcurrentBudgetProvider LoadingBudgetProvider;
        public readonly IConcurrentBudgetProvider FrameTimeBudgetProvider;
        public readonly MemoryBudgetProvider MemoryBudgetProvider;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry,
            IReportsHandlingSettings reportsHandlingSettings,
            ISceneEntityFactory entityFactory,
            ISystemGroupAggregate<IPartitionComponent>.IFactory aggregateFactory,
            IConcurrentBudgetProvider loadingBudgetProvider,
            IConcurrentBudgetProvider frameTimeBudgetProvider,
            MemoryBudgetProvider memoryBudgetProvider)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;
            EntityFactory = entityFactory;
            LoadingBudgetProvider = loadingBudgetProvider;
            AggregateFactory = aggregateFactory;
            FrameTimeBudgetProvider = frameTimeBudgetProvider;
            MemoryBudgetProvider = memoryBudgetProvider;
        }
    }
}
