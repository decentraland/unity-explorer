using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.UpdateGate;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading;
using ECS.Unity.EngineInfo;
using ECS.Unity.Systems;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies singletonDependencies;
        private readonly IPartitionSettings partitionSettings;
        private readonly IReadOnlyCameraSamplingData cameraSamplingData;
        private readonly IExposedCameraData exposedCameraData;
        private readonly IReadOnlyList<IDCLWorldPlugin> plugins;

        public ECSWorldFactory(ECSWorldSingletonSharedDependencies sharedDependencies,
            IPartitionSettings partitionSettings, IReadOnlyCameraSamplingData cameraSamplingData,
            IExposedCameraData exposedCameraData,
            IReadOnlyList<IDCLWorldPlugin> plugins)
        {
            this.plugins = plugins;
            singletonDependencies = sharedDependencies;
            this.partitionSettings = partitionSettings;
            this.cameraSamplingData = cameraSamplingData;
            this.exposedCameraData = exposedCameraData;
        }

        public ECSWorldFacade CreateWorld(in ECSWorldFactoryArgs args)
        {
            ISystemGroupsUpdateGate systemGroupsUpdateGate = args.SystemGroupsUpdateGate;
            ECSWorldInstanceSharedDependencies sharedDependencies = args.SharedDependencies;
            IPartitionComponent scenePartition = args.ScenePartition;

            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            IComponentPoolsRegistry componentPoolsRegistry = singletonDependencies.ComponentPoolsRegistry;

            Entity sceneRootEntity = world.Create(SpecialEntitiesID.SCENE_ROOT_ENTITY, world);
            var persistentEntities = new PersistentEntities(sceneRootEntity);

            sharedDependencies.EntitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRootEntity;

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world, systemGroupsUpdateGate, systemGroupsUpdateGate,
                sharedDependencies.SceneExceptionsHandler);

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPostRenderingSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider));

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);

            foreach (IDCLWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, in persistentEntities, finalizeWorldSystems);

            WriteCameraComponentsSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, exposedCameraData, sceneRootEntity);

            // Prioritization
            PartitionAssetEntitiesSystem.InjectToWorld(ref builder, partitionSettings, scenePartition, cameraSamplingData, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>(), sceneRootEntity);
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, singletonDependencies.LoadingBudgetProvider, singletonDependencies.MemoryBudgetProvider);
            WriteEngineInfoSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);

            DestroyEntitiesSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // Add other systems here
            SystemGroupWorld systemsWorld = builder.Finish(singletonDependencies.AggregateFactory, scenePartition);

            return new ECSWorldFacade(systemsWorld, world, finalizeWorldSystems);
        }
    }
}
