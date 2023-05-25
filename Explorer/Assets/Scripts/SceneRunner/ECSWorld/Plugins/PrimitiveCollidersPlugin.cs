using Arch.Core;
using Arch.SystemGroups;
using ECS.ComponentsPooling;
using ECS.Unity.PrimitiveColliders.Systems;

namespace SceneRunner.ECSWorld.Plugins
{
    public class PrimitiveCollidersPlugin : IECSWorldPlugin
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public PrimitiveCollidersPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies)
        {
            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
        }
    }
}
