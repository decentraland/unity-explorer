using Arch.Core;
using Arch.SystemGroups;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class PrimitiveCollidersPlugin : IECSWorldPlugin
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public PrimitiveCollidersPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            var releaseColliderSystem =
                ReleasePoolableComponentSystem<Collider, PrimitiveColliderComponent>.InjectToWorld(ref builder,
                    componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseColliderSystem);
        }
    }
}
