using CrdtEcsBridge.Components;
using Diagnostics.ReportsHandling;
using ECS.ComponentsPooling;

namespace SceneRunner.ECSWorld
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly IEntityFactory EntityFactory;

        public ECSWorldSingletonSharedDependencies(IComponentPoolsRegistry componentPoolsRegistry, IReportsHandlingSettings reportsHandlingSettings, IEntityFactory entityFactory)
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;
            EntityFactory = entityFactory;
        }
    }
}
