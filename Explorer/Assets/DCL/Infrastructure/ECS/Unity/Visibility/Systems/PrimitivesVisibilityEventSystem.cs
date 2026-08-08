using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.PrimitiveRenderer.Components;

namespace ECS.Unity.Visibility.Systems
{
    /// <summary>
    ///     Applies the initial resolved visibility to just-instantiated primitive meshes.
    ///     <para>
    ///     Deliberately NOT [ThrottlingEnabled]. The create events are written one-shot per instantiated
    ///     entity by InstantiatePrimitiveRenderingSystem, which is budget-deferred
    ///     (IPerformanceBudget instantiationFrameTimeBudget) so a queued primitive can finish
    ///     instantiating on a gate-closed frame, and drained every frame by ClearEntityEventsSystem.
    ///     A throttled consumer would be skipped on that gate-closed frame and the event would be cleared
    ///     before it was ever read, so the primitive's initial visibility would never be applied. Keeping
    ///     this drain on the every-frame cadence closes that gap; the throttleable dirty-driven scans live
    ///     in <see cref="PrimitivesVisibilitySystem" />.
    ///     </para>
    ///     Ordered after the ComponentInstantiationGroup producer and after
    ///     <see cref="PrimitivesVisibilitySystem" /> so events produced this frame are consumed before
    ///     ClearEntityEventsSystem clears the buffer.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(PrimitivesVisibilitySystem))]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class PrimitivesVisibilityEventSystem : VisibilitySystemBase<PrimitiveMeshRendererComponent>
    {
        public PrimitivesVisibilityEventSystem(World world, EntityEventBuffer<PrimitiveMeshRendererComponent> changedMeshes)
            : base(world, changedMeshes)
        {
        }

        protected override void Update(float t)
        {
            ApplyNewlyCreatedRenderables();
        }

        protected override void UpdateVisibilityInternal(in PrimitiveMeshRendererComponent component, bool visible)
        {
            component.MeshRenderer.enabled = visible;
        }
    }
}
