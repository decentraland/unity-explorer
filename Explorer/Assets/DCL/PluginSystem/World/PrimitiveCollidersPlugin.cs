using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class PrimitiveCollidersPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public PrimitiveCollidersPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            var releaseColliderSystem =
                ReleasePoolableComponentSystem<Collider, PrimitiveColliderComponent>.InjectToWorld(ref builder,
                    componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseColliderSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
