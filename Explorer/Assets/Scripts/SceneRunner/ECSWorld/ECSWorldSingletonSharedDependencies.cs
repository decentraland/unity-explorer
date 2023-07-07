using ECS.ComponentsPooling;
using ECS.Prioritization.DeferredLoading;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly ConcurrentLoadingBudgetProvider LoadingBudgetProvider;
        public readonly IConcurrentBudgetProvider InstantiatingBudgetProvider;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry, ConcurrentLoadingBudgetProvider loadingBudgetProvider, IConcurrentBudgetProvider instantiatingBudgetProvider)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            LoadingBudgetProvider = loadingBudgetProvider;
            InstantiatingBudgetProvider = instantiatingBudgetProvider;
        }
    }
}
