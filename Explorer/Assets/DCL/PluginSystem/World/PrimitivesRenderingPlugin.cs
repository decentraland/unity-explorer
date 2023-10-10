using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.ComponentsPooling;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class PrimitivesRenderingPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IConcurrentBudgetProvider capFrameTimeBudgetProvider;

        public PrimitivesRenderingPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            capFrameTimeBudgetProvider = singletonSharedDependencies.FrameTimeBudgetProvider;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiatePrimitiveRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry, capFrameTimeBudgetProvider);
            ReleaseOutdatedRenderingSystem.InjectToWorld(ref builder, componentPoolsRegistry);

            ResetDirtyFlagSystem<PBMeshRenderer>.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MeshRenderer, PrimitiveMeshRendererComponent>.InjectToWorld(
                ref builder, componentPoolsRegistry));

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<IPrimitiveMesh, PrimitiveMeshRendererComponent>.InjectToWorld(
                ref builder, componentPoolsRegistry));
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
