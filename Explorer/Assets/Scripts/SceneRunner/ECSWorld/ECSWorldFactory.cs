using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using ECS.Unity.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld
{
    public class ECSWorldFactory : IECSWorldFactory
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public ECSWorldFactory(IComponentPoolsRegistry componentPoolsRegistry /* Add here all singleton dependencies */)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public ECSWorldFacade CreateWorld(in Dictionary<CRDTEntity, Entity> entitiesMap)
        {
            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            // We create the scene root transform
            var sceneRootTransform = (Transform)componentPoolsRegistry.GetReferenceTypePool<Transform>().Rent();
            sceneRootTransform.name = "SCENE_ROOT";
            world.Create(sceneRootTransform);

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world);
            UpdateTransformSystem.InjectToWorld(ref builder);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, entitiesMap, sceneRootTransform);
            AssertDisconnectedTransformsSystem.InjectToWorld(ref builder);
            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            var releaseSDKComponentsSystem = ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseColliderSystem = ReleasePoolableComponentSystem<PrimitiveColliderComponent>.InjectToWorld(ref builder, componentPoolsRegistry);


            // Add other systems here
            var systemsWorld = builder.Finish();

            return new ECSWorldFacade(systemsWorld, world, releaseSDKComponentsSystem, releaseColliderSystem);
        }
    }
}
