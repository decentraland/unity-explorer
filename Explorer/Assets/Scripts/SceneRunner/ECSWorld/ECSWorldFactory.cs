using Arch.Core;
using Arch.SystemGroups;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.ECSWorld.Plugins;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies singletonDependencies;
        private readonly IReadOnlyList<IECSWorldPlugin> plugins;

        public ECSWorldFactory(ECSWorldSingletonSharedDependencies sharedDependencies, params IECSWorldPlugin[] plugins)
        {
            this.plugins = plugins;
            singletonDependencies = sharedDependencies;
        }

        public ECSWorldFacade CreateWorld(in ECSWorldInstanceSharedDependencies sharedDependencies)
        {
            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            IComponentPoolsRegistry componentPoolsRegistry = singletonDependencies.ComponentPoolsRegistry;

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world);

            foreach (IECSWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies);

            var releaseSDKComponentsSystem = ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseColliderSystem = ReleasePoolableComponentSystem<PrimitiveColliderComponent>.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseTransformSystem = ReleasePoolableComponentSystem<TransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry);

            // Add other systems here
            var systemsWorld = builder.Finish();

            return new ECSWorldFacade(systemsWorld, world, releaseSDKComponentsSystem, releaseColliderSystem, releaseTransformSystem);
        }
    }
}
