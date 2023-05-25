using Arch.Core;
using Arch.SystemGroups;
using ECS.ComponentsPooling;
using ECS.Unity.Systems;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class TransformsPlugin : IECSWorldPlugin
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public TransformsPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies)
        {
            // We create the scene root transform
            Transform sceneRootTransform = componentPoolsRegistry.GetReferenceTypePool<Transform>().Get();
            sceneRootTransform.transform.SetParent(null);
            sceneRootTransform.name = $"SCENE_ROOT_{sharedDependencies.ContentProvider.SceneName}";
            Entity rootTransformEntity = builder.World.Create(new TransformComponent(sceneRootTransform));

            UpdateTransformSystem.InjectToWorld(ref builder);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, builder.World.Reference(rootTransformEntity));
            AssertDisconnectedTransformsSystem.InjectToWorld(ref builder);
        }
    }
}
