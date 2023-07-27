using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.UpdateGate;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading;
using ECS.Unity.Systems;
using SceneRunner.ECSWorld.Plugins;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies singletonDependencies;
        private readonly IPartitionSettings partitionSettings;
        private readonly IReadOnlyCameraSamplingData cameraSamplingData;
        private readonly IReadOnlyList<IECSWorldPlugin> plugins;

        public ECSWorldFactory(ECSWorldSingletonSharedDependencies sharedDependencies,
            IPartitionSettings partitionSettings, IReadOnlyCameraSamplingData cameraSamplingData, IReadOnlyList<IECSWorldPlugin> plugins)
        {
            this.plugins = plugins;
            singletonDependencies = sharedDependencies;
            this.partitionSettings = partitionSettings;
            this.cameraSamplingData = cameraSamplingData;
        }

        public ECSWorldFacade CreateWorld(in ECSWorldFactoryArgs args)
        {
            ISystemGroupsUpdateGate systemGroupsUpdateGate = args.SystemGroupsUpdateGate;
            ECSWorldInstanceSharedDependencies sharedDependencies = args.SharedDependencies;
            IPartitionComponent scenePartition = args.ScenePartition;

            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            IComponentPoolsRegistry componentPoolsRegistry = singletonDependencies.ComponentPoolsRegistry;

            Entity sceneRootEntity = singletonDependencies.EntityFactory.Create(SpecialEntititiesID.SCENE_ROOT_ENTITY, world);

            var persistentEntities = new PersistentEntities(world.Reference(sceneRootEntity));

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world, systemGroupsUpdateGate, systemGroupsUpdateGate,
                sharedDependencies.SceneExceptionsHandler);

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(sharedDependencies.MutexSync, args.SceneStateProvider))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(sharedDependencies.MutexSync, args.SceneStateProvider))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(sharedDependencies.MutexSync, args.SceneStateProvider))
               .InjectCustomGroup(new SyncedPostRenderingSystemGroup(sharedDependencies.MutexSync, args.SceneStateProvider));

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);

            foreach (IECSWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, in persistentEntities, finalizeWorldSystems);

            // Prioritization
            PartitionAssetEntitiesSystem.InjectToWorld(ref builder, partitionSettings, scenePartition, cameraSamplingData, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>(), sceneRootEntity);
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, singletonDependencies.LoadingBudgetProvider);

            DestroyEntitiesSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // Add other systems here
            SystemGroupWorld systemsWorld = builder.Finish(singletonDependencies.AggregateFactory, scenePartition);

            return new ECSWorldFacade(systemsWorld, world, finalizeWorldSystems);
        }
    }
}
