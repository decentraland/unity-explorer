using CrdtEcsBridge.Components;
using Diagnostics.ReportsHandling;
using ECS.ComponentsPooling;
using ECS.Prioritization.DeferredLoading;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly IEntityFactory EntityFactory;
        public readonly IConcurrentBudgetProvider LoadingBudgetProvider;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry, IReportsHandlingSettings reportsHandlingSettings, IEntityFactory entityFactory,
            IConcurrentBudgetProvider loadingBudgetProvider)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;
            EntityFactory = entityFactory;
            LoadingBudgetProvider = loadingBudgetProvider;
        }
    }
}
