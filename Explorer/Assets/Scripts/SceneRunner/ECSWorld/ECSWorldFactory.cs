using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.UpdateGate;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.ComponentsPooling.Systems;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading;
using ECS.Unity.EngineInfo;
using ECS.Unity.Systems;
using System.Collections.Generic;
using SystemGroups.Visualiser;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies singletonDependencies;
        private readonly IPartitionSettings partitionSettings;
        private readonly IReadOnlyCameraSamplingData cameraSamplingData;
        private readonly IReadOnlyList<IDCLWorldPlugin> plugins;

        public ECSWorldFactory(
            ECSWorldSingletonSharedDependencies sharedDependencies,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData cameraSamplingData,
            IReadOnlyList<IDCLWorldPlugin> plugins
        )
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
            IPartitionComponent scenePartition = sharedDependencies.ScenePartition;

            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            IComponentPoolsRegistry componentPoolsRegistry = singletonDependencies.ComponentPoolsRegistry;

            PersistentEntities persistentEntities = CreateReservedEntities(world, sharedDependencies);

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world, systemGroupsUpdateGate, systemGroupsUpdateGate,
                sharedDependencies.SceneExceptionsHandler);

            var mutex = sharedDependencies.MutexSync;

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(mutex, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(mutex, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(mutex, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPreRenderingSystemGroup(mutex, sharedDependencies.SceneStateProvider));

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);
            var isCurrentListeners = new List<ISceneIsCurrentListener>(32);

            foreach (IDCLWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, in persistentEntities, finalizeWorldSystems, isCurrentListeners);

            // Prioritization
            PartitionAssetEntitiesSystem.InjectToWorld(ref builder, partitionSettings, scenePartition, cameraSamplingData, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>().EnsureNotNull(), persistentEntities.SceneRoot);
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, singletonDependencies.LoadingBudget, singletonDependencies.MemoryBudget);
            WriteEngineInfoSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);

            ClearEntityEventsSystem.InjectToWorld(ref builder, sharedDependencies.EntityEventsBuilder);
            DestroyEntitiesSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // These system will prevent changes from the JS scenes to squeeze in between different stages of the PlayerLoop at the same frame
            LockECSSystem.InjectToWorld(ref builder, mutex);
            UnlockECSSystem.InjectToWorld(ref builder, mutex);

            SystemGroupWorld systemsWorld = builder.Finish(singletonDependencies.AggregateFactory, scenePartition).EnsureNotNull();

            SystemGroupSnapshot.Instance!.Register(args.SceneData.SceneShortInfo.ToString(), systemsWorld);
            singletonDependencies.SceneMapping.Register(args.SceneData.SceneShortInfo.Name, args.SceneData.Parcels, world);

            return new ECSWorldFacade(systemsWorld, world, persistentEntities, finalizeWorldSystems, isCurrentListeners);
        }

        private static PersistentEntities CreateReservedEntities(World world, ECSWorldInstanceSharedDependencies sharedDependencies)
        {
            Entity sceneRootEntity = world.Create(new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY), new SceneRootComponent(), RemovedComponents.CreateDefault());
            Entity playerEntity = world.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY), RemovedComponents.CreateDefault());
            Entity cameraEntity = world.Create(new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY), RemovedComponents.CreateDefault());

            sharedDependencies.EntitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRootEntity;
            sharedDependencies.EntitiesMap[SpecialEntitiesID.PLAYER_ENTITY] = playerEntity;
            sharedDependencies.EntitiesMap[SpecialEntitiesID.CAMERA_ENTITY] = cameraEntity;

            return new PersistentEntities(playerEntity, cameraEntity, sceneRootEntity);
        }
    }
}
