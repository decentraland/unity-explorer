using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.Systems;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.World
{
    public class TransformsPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public TransformsPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies, ObjectProxy<Arch.Core.World> globalWorldProxy)
        {
            this.globalWorldProxy = globalWorldProxy;
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<Transform>(onRelease: transform =>
            {
                transform.ResetLocalTRS();
                transform.gameObject.layer = 0;
            });
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            CreateReservedTransforms(builder, sharedDependencies, persistentEntities);

            UpdateTransformSystem.InjectToWorld(ref builder);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot, sharedDependencies.SceneData.SceneShortInfo);
            AssertDisconnectedTransformsSystem.InjectToWorld(ref builder);
            SyncGlobalTransformSystem.InjectToWorld(ref builder, globalWorldProxy, in persistentEntities.Camera, in persistentEntities.Player);

            var releaseTransformSystem =
                ReleasePoolableComponentSystem<Transform, TransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseTransformSystem);
        }

        private void CreateReservedTransforms(ArchSystemsWorldBuilder<Arch.Core.World> builder,
            ECSWorldInstanceSharedDependencies sharedDependencies, PersistentEntities persistentEntities)
        {
            Transform sceneRootTransform = GetNewTransform(sharedDependencies);
            sceneRootTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel}_{sharedDependencies.SceneData.SceneShortInfo.Name}";
            builder.World.Add(persistentEntities.SceneRoot, new TransformComponent(sceneRootTransform));

            Transform playerTransform = GetNewTransform(sharedDependencies, sceneRootTransform);
            playerTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel} PLAYER_ENTITY";
            builder.World.Add(persistentEntities.Player, new TransformComponent(playerTransform), new PlayerTransformSync());

            Transform cameraTransform = GetNewTransform(sharedDependencies, sceneRootTransform);
            cameraTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel} CAMERA_ENTITY";
            builder.World.Add(persistentEntities.Camera, new TransformComponent(cameraTransform), new CameraTransformSync());
        }

        private Transform GetNewTransform(ECSWorldInstanceSharedDependencies sharedDependencies, Transform? transform = null)
        {
            Transform sceneRootTransform = componentPoolsRegistry.GetReferenceTypePool<Transform>().Get();
            sceneRootTransform.SetParent(transform);
            sceneRootTransform.position = sharedDependencies.SceneData.Geometry.BaseParcelPosition;
            sceneRootTransform.rotation = Quaternion.identity;
            sceneRootTransform.localScale = Vector3.one;
            return sceneRootTransform;
        }
    }
}
