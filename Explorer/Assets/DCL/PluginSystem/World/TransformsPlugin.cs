﻿using Arch.SystemGroups;
using DCL.PerformanceAndDiagnostics.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
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
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public TransformsPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<Transform>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // We create the scene root transform
            Transform sceneRootTransform = componentPoolsRegistry.GetReferenceTypePool<Transform>().Get();

            sceneRootTransform.SetParent(null);

            Vector3 basePosition = ParcelMathHelper.GetPositionByParcelPosition(sharedDependencies.SceneData.SceneShortInfo.BaseParcel);
            sceneRootTransform.position = basePosition;
            sceneRootTransform.rotation = Quaternion.identity;
            sceneRootTransform.localScale = Vector3.one;

            sceneRootTransform.name = $"{sharedDependencies.SceneData.SceneShortInfo.BaseParcel}_{sharedDependencies.SceneData.SceneShortInfo.Name}";
            builder.World.Add(persistentEntities.SceneRoot, new TransformComponent(sceneRootTransform));

            UpdateTransformSystem.InjectToWorld(ref builder);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, sharedDependencies.EntitiesMap, persistentEntities.SceneRoot);
            AssertDisconnectedTransformsSystem.InjectToWorld(ref builder);

            var releaseTransformSystem =
                ReleasePoolableComponentSystem<Transform, TransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry);

            finalizeWorldSystems.Add(releaseTransformSystem);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            UpdateTransformSystem.InjectToWorld(ref builder);
            InstantiateTransformSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ParentingTransformSystem.InjectToWorld(ref builder, dependencies.FakeEntitiesMap, dependencies.SceneRoot);
            ReleasePoolableComponentSystem<Transform, TransformComponent>.InjectToWorld(ref builder, componentPoolsRegistry);
        }
    }
}
