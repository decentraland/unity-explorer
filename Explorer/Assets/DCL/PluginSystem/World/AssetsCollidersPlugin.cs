using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using ECS.Unity.SceneBoundsChecker;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AssetsCollidersPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AssetsCollidersPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;

            componentPoolsRegistry.AddGameObjectPool<MeshCollider>();
            componentPoolsRegistry.AddGameObjectPool<BoxCollider>();
            componentPoolsRegistry.AddGameObjectPool<SphereCollider>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EntityCollidersSceneCache);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.EntityCollidersSceneCache);

            CheckColliderBoundsSystem.InjectToWorld(ref builder, sharedDependencies.ScenePartition, sharedDependencies.SceneData.Geometry);

            var releaseColliderSystem =
                ReleasePoolableComponentSystem<Collider, PrimitiveColliderComponent>.InjectToWorld(ref builder,
                    componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseColliderSystem);
        }
    }
}
