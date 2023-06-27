using Diagnostics.ReportsHandling;
using ECS.ComponentsPooling;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry, IReportsHandlingSettings reportsHandlingSettings)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;
        }
    }
}
