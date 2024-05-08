using Arch.Core;
using Arch.SystemGroups;
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
using ECS.LifeCycle.Systems;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.StreamableLoading.DeferredLoading;
using ECS.Unity.EngineInfo;
using ECS.Unity.Systems;
using System.Collections.Generic;
using SystemGroups.Visualiser;
using Utility.Multithreading;
using GatherGltfAssetsSystem = ECS.SceneLifeCycle.Systems.GatherGltfAssetsSystem;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies singletonDependencies;
        private readonly IPartitionSettings partitionSettings;
        private readonly IReadOnlyCameraSamplingData cameraSamplingData;
        private readonly IReadOnlyList<IDCLWorldPlugin> plugins;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        public ECSWorldFactory(
            ECSWorldSingletonSharedDependencies sharedDependencies,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData cameraSamplingData,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            IReadOnlyList<IDCLWorldPlugin> plugins
        )
        {
            this.plugins = plugins;
            singletonDependencies = sharedDependencies;
            this.partitionSettings = partitionSettings;
            this.cameraSamplingData = cameraSamplingData;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        public ECSWorldFacade CreateWorld(in ECSWorldFactoryArgs args)
        {
            ISystemGroupsUpdateGate systemGroupsUpdateGate = args.SystemGroupsUpdateGate;
            ECSWorldInstanceSharedDependencies sharedDependencies = args.SharedDependencies;
            IPartitionComponent scenePartition = sharedDependencies.ScenePartition;

            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            IComponentPoolsRegistry componentPoolsRegistry = singletonDependencies.ComponentPoolsRegistry;

            Entity sceneRootEntity = world.Create(new SceneRootComponent(), world);
            var persistentEntities = new PersistentEntities(sceneRootEntity);

            sharedDependencies.EntitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRootEntity;

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world, systemGroupsUpdateGate, systemGroupsUpdateGate,
                sharedDependencies.SceneExceptionsHandler);

            var mutex = sharedDependencies.MutexSync; //sharedDependencies.MutexSync;

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(mutex, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(mutex, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(mutex, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPostRenderingSystemGroup(mutex, sharedDependencies.SceneStateProvider));

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);
            var isCurrentListeners = new List<ISceneIsCurrentListener>(32);

            foreach (IDCLWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, in persistentEntities, finalizeWorldSystems, isCurrentListeners);

            // Prioritization
            PartitionAssetEntitiesSystem.InjectToWorld(ref builder, partitionSettings, scenePartition, cameraSamplingData, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>().EnsureNotNull(), sceneRootEntity);
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, singletonDependencies.LoadingBudget, singletonDependencies.MemoryBudget);
            WriteEngineInfoSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);

            GatherGltfAssetsSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, args.SceneData);

            ClearEntityEventsSystem.InjectToWorld(ref builder, sharedDependencies.EntityEventsBuilder);
            DestroyEntitiesSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // These system will prevent changes from the JS scenes to squeeze in between different stages of the PlayerLoop at the same frame
            LockECSSystem.InjectToWorld(ref builder, mutex);
            UnlockECSSystem.InjectToWorld(ref builder, mutex);

            SystemGroupWorld systemsWorld = builder.Finish(singletonDependencies.AggregateFactory, scenePartition).EnsureNotNull();

            SystemGroupSnapshot.Instance!.Register(args.SceneData.SceneShortInfo.ToString(), systemsWorld);

            return new ECSWorldFacade(systemsWorld, world, finalizeWorldSystems, isCurrentListeners);
        }
    }
}
