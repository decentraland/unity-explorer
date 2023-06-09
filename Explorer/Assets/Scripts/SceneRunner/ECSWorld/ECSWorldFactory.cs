using Arch.Core;
using Arch.SystemGroups;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
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

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);

            foreach (IECSWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, finalizeWorldSystems);

            DestroyEntitiesSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // Add other systems here
            var systemsWorld = builder.Finish();

            return new ECSWorldFacade(systemsWorld, world, finalizeWorldSystems);
        }
    }
}
