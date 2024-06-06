using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.Time;
using ECS.Abstract;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using ECS.Unity.SceneBoundsChecker;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AssetsCollidersPlugin : IDCLWorldPluginWithoutSettings
    {
        private const int COLLIDERS_VISIBILITY_EVENTS_CAPACITY = 500;

        private readonly IPhysicsTickProvider physicsTickProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        static AssetsCollidersPlugin()
        {
            EntityEventBuffer<PrimitiveColliderComponent>.Register(COLLIDERS_VISIBILITY_EVENTS_CAPACITY);
        }

        public AssetsCollidersPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies, IPhysicsTickProvider physicsTickProvider)
        {
            this.physicsTickProvider = physicsTickProvider;
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;

            componentPoolsRegistry.AddGameObjectPool<MeshCollider>();
            componentPoolsRegistry.AddGameObjectPool<BoxCollider>();
            componentPoolsRegistry.AddGameObjectPool<SphereCollider>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<PrimitiveColliderComponent>();

            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EntityCollidersSceneCache);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EntityCollidersSceneCache);
            PrimitiveCollidersVisibilitySystem.InjectToWorld(ref builder, buffer);

            CheckColliderBoundsSystem.InjectToWorld(ref builder, sharedDependencies.ScenePartition, sharedDependencies.SceneData.Geometry, physicsTickProvider);

            var releaseColliderSystem =
                ReleasePoolableComponentSystem<Collider, PrimitiveColliderComponent>.InjectToWorld(ref builder,
                    componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseColliderSystem);
        }
    }
}
