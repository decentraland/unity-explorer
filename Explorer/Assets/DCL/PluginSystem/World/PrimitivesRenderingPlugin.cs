using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using ECS.Abstract;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.Systems;
using ECS.Unity.Visibility.Systems;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.World
{
    public class PrimitivesRenderingPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IPerformanceBudget capFrameTimeBudget;

        static PrimitivesRenderingPlugin()
        {
            EntityEventBuffer<PrimitiveMeshRendererComponent>.Register(500);
        }

        public PrimitivesRenderingPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            capFrameTimeBudget = singletonSharedDependencies.FrameTimeBudget;

            componentPoolsRegistry.AddComponentPool<BoxPrimitive>();
            componentPoolsRegistry.AddComponentPool<SpherePrimitive>();
            componentPoolsRegistry.AddComponentPool<PlanePrimitive>();
            componentPoolsRegistry.AddComponentPool<CylinderPrimitive>();
            componentPoolsRegistry.AddGameObjectPool(MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<PrimitiveMeshRendererComponent>();

            InstantiatePrimitiveRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry, capFrameTimeBudget, sharedDependencies.SceneData, buffer);
            ReleaseOutdatedRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            PrimitivesVisibilitySystem.InjectToWorld(ref builder, buffer);

            ResetDirtyFlagSystem<PBMeshRenderer>.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MeshRenderer, PrimitiveMeshRendererComponent>.InjectToWorld(
                ref builder, componentPoolsRegistry));

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<IPrimitiveMesh, PrimitiveMeshRendererComponent>.InjectToWorld(
                ref builder, componentPoolsRegistry));
        }
    }
}
