using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveRenderer.Components;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    /// <summary>
    ///     Releases the previous collider if its type was changed at runtime
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    public partial class ReleaseOutdatedRenderingSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;

        internal ReleaseOutdatedRenderingSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.poolsRegistry = poolsRegistry;
        }

        protected override void Update(float t)
        {
            ValidateRenderingQuery(World);
            HandleComponentRemovalQuery(World);
            World.Remove<PrimitiveMeshRendererComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBMeshRenderer))]
        private void HandleComponentRemoval(ref PrimitiveMeshRendererComponent rendererComponent)
        {
            if (poolsRegistry.TryGetPool(rendererComponent.PrimitiveMesh.GetType(), out IComponentPool componentPool))
                componentPool.Release(rendererComponent.PrimitiveMesh);
        }

        [Query]
        private void ValidateRendering(ref PBMeshRenderer meshRenderer,
            ref PrimitiveMeshRendererComponent rendererComponent)
        {
            if (meshRenderer.IsDirty && meshRenderer.MeshCase != rendererComponent.SDKType)
            {
                if (poolsRegistry.TryGetPool(rendererComponent.PrimitiveMesh.GetType(), out IComponentPool componentPool))
                    componentPool.Release(rendererComponent.PrimitiveMesh);

                // it will be a signal to instantiate a new renderer
                rendererComponent.PrimitiveMesh = null;
            }
        }
    }
}
