﻿using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class PrimitivesRenderingPlugin : IECSWorldPlugin
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public PrimitivesRenderingPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiatePrimitiveRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry);
            ReleaseOutdatedRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            ResetDirtyFlagSystem<PBMeshRenderer>.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MeshRenderer, PrimitiveMeshRendererComponent>.InjectToWorld(
                ref builder, componentPoolsRegistry));

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<IPrimitiveMesh, PrimitiveMeshRendererComponent>.InjectToWorld(
                ref builder, componentPoolsRegistry));
        }
    }
}
