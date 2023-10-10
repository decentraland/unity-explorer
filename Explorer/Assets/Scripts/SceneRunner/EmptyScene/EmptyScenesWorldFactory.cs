using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.Groups;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.DeferredLoading;
using ECS.Unity.Systems;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace SceneRunner.EmptyScene
{
    /// <summary>
    ///     Creates a subset of systems that is suitable for handling transforms and gltf loading
    /// </summary>
    public class EmptyScenesWorldFactory : IEmptyScenesWorldFactory
    {
        private readonly ECSWorldSingletonSharedDependencies sharedDependencies;
        private readonly IReadOnlyList<IDCLWorldPlugin> ecsWorldPlugins;

        public EmptyScenesWorldFactory(
            ECSWorldSingletonSharedDependencies sharedDependencies,
            IReadOnlyList<IDCLWorldPlugin> ecsWorldPlugins)
        {
            this.sharedDependencies = sharedDependencies;
            this.ecsWorldPlugins = ecsWorldPlugins;
        }

        public EmptyScenesWorld Create(EmptySceneData emptySceneData)
        {
            var world = World.Create();
            var mutex = new MutexSync();

            // fake map for compatibility
            var fakeEntitiesMap = new Dictionary<CRDTEntity, Entity>();

            // Create all systems and add them to the world
            // no gate, no synchronization, no exceptions handling
            var builder = new ArchSystemsWorldBuilder<World>(world);
            ISceneStateProvider stateProvider = new SceneStateProvider();
            stateProvider.State = SceneState.Running;

            builder
               .InjectCustomGroup(new SyncedInitializationSystemGroup(mutex, stateProvider))
               .InjectCustomGroup(new SyncedSimulationSystemGroup(mutex, stateProvider))
               .InjectCustomGroup(new SyncedPresentationSystemGroup(mutex, stateProvider))
               .InjectCustomGroup(new SyncedPostRenderingSystemGroup(mutex, stateProvider));

            // Root for all empty scenes
            Transform emptyScenesRoot = new GameObject("Empty Scenes").transform;
            emptyScenesRoot.ResetLocalTRS();

            // This entity may be further enriched by plugins
            Entity sceneRootEntity = world.Create(SpecialEntitiesID.SCENE_ROOT_ENTITY, new SceneRootComponent(), new TransformComponent(emptyScenesRoot));

            SyncEmptyScenesPartitionSystem.InjectToWorld(ref builder);
            DestroyEntitiesSystem.InjectToWorld(ref builder);

            // No partitioning - will be inherited from the parent
            AssetsDeferredLoadingSystem.InjectToWorld(ref builder, sharedDependencies.LoadingBudgetProvider);

            var dependencies = new EmptyScenesWorldSharedDependencies(
                fakeEntitiesMap,
                sceneRootEntity,
                emptySceneData,
                mutex
            );

            for (var i = 0; i < ecsWorldPlugins.Count; i++)
                ecsWorldPlugins[i].InjectToEmptySceneWorld(ref builder, in dependencies);

            return new EmptyScenesWorld(builder.Finish(), fakeEntitiesMap, world, mutex);
        }
    }
}
