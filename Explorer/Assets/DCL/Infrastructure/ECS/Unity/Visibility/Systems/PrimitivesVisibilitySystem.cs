using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;

using ECS.Unity.PrimitiveRenderer.Components;


namespace ECS.Unity.Visibility.Systems
{
    /// <summary>
    ///     Dirty-driven primitive-mesh visibility application. Throttled to match the
    ///     [ThrottlingEnabled] VisibilityPropagationSystem that flips ResolvedVisibilityComponent.IsDirty
    ///     — on gate-closed frames nothing could have set IsDirty, so the full renderer-archetype scan is
    ///     skipped. The per-instantiation initial-visibility event drain is handled every frame by the
    ///     un-throttled <see cref="PrimitivesVisibilityEventSystem" /> so those one-shot events are never
    ///     dropped (InstantiatePrimitiveRenderingSystem is budget-deferred and can complete on a
    ///     gate-closed frame).
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.PRIMITIVE_MESHES)]
    public partial class PrimitivesVisibilitySystem : VisibilitySystemBase<PrimitiveMeshRendererComponent>
    {
        public PrimitivesVisibilitySystem(World world, EntityEventBuffer<PrimitiveMeshRendererComponent> changedMeshes)
            : base(world, changedMeshes)
        {
        }

        // Throttled path only: the idle-skippable dirty scans + removal. The every-frame create-event
        // drain is handled by PrimitivesVisibilityEventSystem so it is never dropped on a gate-closed frame.
        protected override void Update(float t)
        {
            UpdateDirtyDrivenVisibility();
            HandleRemovedVisibilityComponents();
        }

        protected override void UpdateVisibilityInternal(in PrimitiveMeshRendererComponent component, bool visible)
        {
            component.MeshRenderer.enabled = visible;
        }
    }
}
