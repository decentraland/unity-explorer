using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.UpdateGate;
using DCL.CharacterCamera;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
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
using GatherGltfAssetsSystem = ECS.SceneLifeCycle.Systems.GatherGltfAssetsSystem;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies singletonDependencies;
        private readonly IPartitionSettings partitionSettings;
        private readonly IReadOnlyCameraSamplingData cameraSamplingData;
        private readonly IExposedCameraData exposedCameraData;
        private readonly IReadOnlyList<IDCLWorldPlugin> plugins;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        public ECSWorldFactory(ECSWorldSingletonSharedDependencies sharedDependencies,
            IPartitionSettings partitionSettings, IReadOnlyCameraSamplingData cameraSamplingData,
            IExposedCameraData exposedCameraData,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            IReadOnlyList<IDCLWorldPlugin> plugins)
        {
            this.plugins = plugins;
            singletonDependencies = sharedDependencies;
            this.partitionSettings = partitionSettings;
            this.cameraSamplingData = cameraSamplingData;
            this.exposedCameraData = exposedCameraData;
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

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider))
               .InjectCustomGroup(new SyncedPostRenderingSystemGroup(sharedDependencies.MutexSync, sharedDependencies.SceneStateProvider));

            var finalizeWorldSystems = new List<IFinalizeWorldSystem>(32);

            foreach (IDCLWorldPlugin worldPlugin in plugins)
                worldPlugin.InjectToWorld(ref builder, in sharedDependencies, in persistentEntities, finalizeWorldSystems);

            // Prioritization
            PartitionAssetEntitiesSystem.InjectToWorld(ref builder, partitionSettings, scenePartition, cameraSamplingData, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>(), sceneRootEntity);
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, singletonDependencies.LoadingBudget, singletonDependencies.MemoryBudget);
            WriteEngineInfoSystem.InjectToWorld(ref builder, sharedDependencies.SceneStateProvider, sharedDependencies.EcsToCRDTWriter);

            GatherGltfAssetsSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, args.SceneData);

            DestroyEntitiesSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry));
            finalizeWorldSystems.Add(ReleaseRemovedComponentsSystem.InjectToWorld(ref builder));

            // Add other systems here
            SystemGroupWorld systemsWorld = builder.Finish(singletonDependencies.AggregateFactory, scenePartition);

            SystemGroupSnapshot.Instance.Register(args.SceneData.SceneShortInfo.ToString(), systemsWorld);

            return new ECSWorldFacade(systemsWorld, world, finalizeWorldSystems);
        }
    }
}
