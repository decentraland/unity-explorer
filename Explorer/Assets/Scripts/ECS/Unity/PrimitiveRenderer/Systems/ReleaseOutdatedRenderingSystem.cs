using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Materials;
using ECS.Unity.PrimitiveRenderer.Components;

namespace ECS.Unity.PrimitiveRenderer.Systems
{
    /// <summary>
    ///     Releases the previous collider if its type was changed at runtime
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    [ThrottlingEnabled]
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
        [None(typeof(PBMeshRenderer), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref PrimitiveMeshRendererComponent rendererComponent)
        {
            rendererComponent.Release(poolsRegistry);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ValidateRendering(ref PBMeshRenderer meshRenderer, ref PrimitiveMeshRendererComponent rendererComponent)
        {
            if (rendererComponent.ShouldInvalidate(meshRenderer))
                rendererComponent.PrepareToReinstall(poolsRegistry);
        }
    }
}
