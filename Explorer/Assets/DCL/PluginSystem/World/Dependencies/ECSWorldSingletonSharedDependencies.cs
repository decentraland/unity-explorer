using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using SceneRunner.Mapping;

namespace DCL.PluginSystem.World.Dependencies
{
    public readonly struct ECSWorldSingletonSharedDependencies
    {
        public readonly IComponentPoolsRegistry ComponentPoolsRegistry;
        public readonly IReportsHandlingSettings ReportsHandlingSettings;
        public readonly ISystemGroupAggregate<IPartitionComponent>.IFactory AggregateFactory;
        public readonly ISceneEntityFactory EntityFactory;
        public readonly ISceneMapping SceneMapping;
        public readonly IReleasablePerformanceBudget LoadingBudget;
        public readonly FrameTimeCapBudget FrameTimeBudget;
        public readonly MemoryBudget MemoryBudget;

        public ECSWorldSingletonSharedDependencies(
            IComponentPoolsRegistry componentPoolsRegistry,
            IReportsHandlingSettings reportsHandlingSettings,
            ISceneEntityFactory entityFactory,
            ISystemGroupAggregate<IPartitionComponent>.IFactory aggregateFactory,
            IReleasablePerformanceBudget loadingBudget,
            FrameTimeCapBudget frameTimeBudget,
            MemoryBudget memoryBudget,
            ISceneMapping sceneMapping
        )
        {
            ComponentPoolsRegistry = componentPoolsRegistry;
            ReportsHandlingSettings = reportsHandlingSettings;

            EntityFactory = entityFactory;
            AggregateFactory = aggregateFactory;

            LoadingBudget = loadingBudget;
            FrameTimeBudget = frameTimeBudget;
            MemoryBudget = memoryBudget;
            SceneMapping = sceneMapping;
        }
    }
}
