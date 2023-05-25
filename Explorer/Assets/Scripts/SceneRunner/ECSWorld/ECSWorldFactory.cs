using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.Systems;
using ECS.Unity.Systems;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
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

        public ECSWorldFacade CreateWorld(IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, string sceneName = null)
        {
            // Worlds uses Pooled Collections under the hood so the memory impact is minimized
            var world = World.Create();

            // We create the scene root transform
            Transform sceneRootTransform = componentPoolsRegistry.GetReferenceTypePool<Transform>().Get();
            sceneRootTransform.transform.SetParent(null);
            sceneRootTransform.name = $"SCENE_ROOT_{sceneName}";
            Entity rootTransformEntity = world.Create(new TransformComponent(sceneRootTransform), CRDTEntity.Create(0, 0));

            // Create all systems and add them to the world
            var builder = new ArchSystemsWorldBuilder<World>(world);
            UpdateTransformSystem.InjectToWorld(ref builder);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, entitiesMap, world.Reference(rootTransformEntity));
            AssertDisconnectedTransformsSystem.InjectToWorld(ref builder);
            InstantiatePrimitiveColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ReleaseOutdatedColliderSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            InstantiatePrimitiveRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            var releaseSDKComponentsSystem = ReleaseReferenceComponentsSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseColliderSystem = ReleasePoolableComponentSystem<PrimitiveColliderComponent>.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseTransformSystem = ReleasePoolableComponentSystem<TransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseRendererSystem = ReleasePoolableComponentSystem<PrimitiveRendererComponent>.InjectToWorld(ref builder, componentPoolsRegistry);
            var releaseMeshSystem = ReleasePoolableComponentSystem<PrimitiveMeshComponent>.InjectToWorld(ref builder, componentPoolsRegistry);


            // Add other systems here
            var systemsWorld = builder.Finish();

            return new ECSWorldFacade(systemsWorld, world, releaseSDKComponentsSystem, releaseColliderSystem, releaseTransformSystem, releaseRendererSystem, releaseMeshSystem);
        }
    }
}
