using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using Decentraland.Common;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.Unity.Systems;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.PluginSystem.World
{
    public class TransformsPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly ExposedTransform exposedPlayerTransform;
        private readonly ExposedCameraData exposedCameraData;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IComponentPool<Transform> transformPool;

        public TransformsPlugin(
            ECSWorldSingletonSharedDependencies singletonSharedDependencies,
            ExposedTransform exposedPlayerTransform,
            ExposedCameraData exposedCameraData)
        {
            this.exposedPlayerTransform = exposedPlayerTransform;
            this.exposedCameraData = exposedCameraData;

            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;

            transformPool = componentPoolsRegistry.AddGameObjectPool<Transform>(onRelease: transform =>
            {
                transform.ResetLocalTRS();
                transform.gameObject.layer = 0;
            });
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            CreateReservedTransforms(builder, sharedDependencies, persistentEntities);

            UpdateTransformSystem.InjectToWorld(ref builder, sharedDependencies.EcsGroupThrottler, sharedDependencies.EcsSystemsGate);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot);
            AssertDisconnectedTransformsSystem.InjectToWorld(ref builder);
            SyncGlobalTransformSystem.InjectToWorld(ref builder, in persistentEntities.Camera, in persistentEntities.Player, exposedPlayerTransform, exposedCameraData);

            var releaseTransformSystem =
                ReleasePoolableComponentSystem<Transform, TransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseTransformSystem);
        }

        private void CreateReservedTransforms(ArchSystemsWorldBuilder<Arch.Core.World> builder,
            ECSWorldInstanceSharedDependencies sharedDependencies, PersistentEntities persistentEntities)
        {
            //The scene container, which is only modified by the client, starts in a position that cannot be seen by the player. Once it finished loading
            //in GatherGLTFAssetSystem.cs, it will be moved to the correct position.
            var sceneRootContainerTransform = GetNewTransform(position: new Vector3(0, -10000, 0));
            sceneRootContainerTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel}_{sharedDependencies.SceneData.SceneShortInfo.Name}_Container";
            builder.World.Add(persistentEntities.SceneContainer, new TransformComponent(sceneRootContainerTransform));

            Transform sceneRootTransform = GetNewTransform(sceneRootContainerTransform);
            sceneRootTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel}_{sharedDependencies.SceneData.SceneShortInfo.Name}_SceneRoot";
            builder.World.Add(persistentEntities.SceneRoot, new TransformComponent(sceneRootTransform));

            Transform playerTransform = GetNewTransform(sceneRootTransform);
            playerTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel} PLAYER_ENTITY";
            builder.World.Add(persistentEntities.Player, new TransformComponent(playerTransform));

            Transform cameraTransform = GetNewTransform(sceneRootTransform);
            cameraTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel} CAMERA_ENTITY";
            builder.World.Add(persistentEntities.Camera, new TransformComponent(cameraTransform));
        }

        private Transform GetNewTransform(Transform? transform = null, Vector3 position = default)
        {
            Transform sceneRootTransform = transformPool.Get();
            sceneRootTransform.SetParent(transform);
            sceneRootTransform.localPosition = position;
            sceneRootTransform.rotation = Quaternion.identity;
            sceneRootTransform.localScale = Vector3.one;
            return sceneRootTransform;
        }
    }
}
