using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.UpdateGate;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.DeferredLoading;
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

        public ECSWorldFacade CreateWorld(in ECSWorldInstanceSharedDependencies sharedDependencies, in ISystemGroupsUpdateGate systemGroupsUpdateGate)
        {
            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            IComponentPoolsRegistry componentPoolsRegistry = singletonDependencies.ComponentPoolsRegistry;

            Entity sceneRootEntity = singletonDependencies.EntityFactory.Create(SpecialEntititiesID.SCENE_ROOT_ENTITY, world);

            var persistentEntities = new PersistentEntities(world.Reference(sceneRootEntity));

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world, systemGroupsUpdateGate, systemGroupsUpdateGate,
                sharedDependencies.SceneExceptionsHandler);

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(sharedDependencies.MutexSync))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(sharedDependencies.MutexSync))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(sharedDependencies.MutexSync))
               .InjectCustomGroup(new SyncedPostRenderingSystemGroup(sharedDependencies.MutexSync));

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);

            foreach (IECSWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, in persistentEntities, finalizeWorldSystems);

            // Deferred loading
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, singletonDependencies.LoadingBudgetProvider);

            DestroyEntitiesSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // Add other systems here
            var systemsWorld = builder.Finish();

            return new ECSWorldFacade(systemsWorld, world, finalizeWorldSystems);
        }
    }
}
