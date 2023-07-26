using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using Diagnostics.ReportsHandling;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly ISystemGroupAggregate<IPartitionComponent>.IFactory AggregateFactory;
        public readonly IEntityFactory EntityFactory;
        public readonly IConcurrentBudgetProvider LoadingBudgetProvider;
        public readonly IConcurrentBudgetProvider FrameTimeCapBudgetProvider;


        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry,
            IReportsHandlingSettings reportsHandlingSettings,
            IEntityFactory entityFactory,
            ISystemGroupAggregate<IPartitionComponent>.IFactory aggregateFactory,
            IConcurrentBudgetProvider loadingBudgetProvider,
            IConcurrentBudgetProvider frameTimeCapBudgetProvider)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;
            EntityFactory = entityFactory;
            LoadingBudgetProvider = loadingBudgetProvider;
            AggregateFactory = aggregateFactory;
            FrameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
        }
    }
}
