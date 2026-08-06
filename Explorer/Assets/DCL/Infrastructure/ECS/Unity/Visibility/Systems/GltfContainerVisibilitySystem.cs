using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Components;

namespace ECS.Unity.Visibility.Systems
{
    /// <summary>
    ///     Dirty-driven GLTF visibility application. Throttled to match the [ThrottlingEnabled]
    ///     VisibilityPropagationSystem that flips ResolvedVisibilityComponent.IsDirty — on gate-closed
    ///     frames nothing could have set IsDirty, so the full renderer-archetype scan is skipped.
    ///     The per-load initial-visibility event drain is handled every frame by the un-throttled
    ///     <see cref="GltfContainerVisibilityEventSystem" /> so those one-shot events are never dropped.
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [ThrottlingEnabled]
    public partial class GltfContainerVisibilitySystem : VisibilitySystemBase<GltfContainerComponent>
    {
        internal GltfContainerVisibilitySystem(World world, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world, eventsBuffer)
        {

        }

        // Throttled path only: the idle-skippable dirty scans + removal. The every-frame create-event
        // drain is handled by GltfContainerVisibilityEventSystem so it is never dropped on a gate-closed frame.
        protected override void Update(float t)
        {
            UpdateDirtyDrivenVisibility();
            HandleRemovedVisibilityComponents();
        }

        protected override void UpdateVisibilityInternal(in GltfContainerComponent component, bool visible)
        {
            // we have several states that are notified with events
            if (component.State != LoadingState.Finished) return;

            component.Promise.Result!.Value.Asset!.SetRenderersActive(visible);
        }
    }
}
