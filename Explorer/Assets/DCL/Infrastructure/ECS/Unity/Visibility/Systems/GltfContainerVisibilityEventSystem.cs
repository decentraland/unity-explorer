using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Components;

namespace ECS.Unity.Visibility.Systems
{
    /// <summary>
    ///     Applies the initial resolved visibility to just-loaded GLTF containers.
    ///     <para>
    ///     Deliberately NOT [ThrottlingEnabled]. The create events are written one-shot by
    ///     FinalizeGltfContainerLoadingSystem on whatever frame the async promise resolves — routinely a
    ///     gate-closed frame for distant/throttled scenes — and drained every frame by
    ///     ClearEntityEventsSystem. A throttled consumer would be skipped on that gate-closed frame and
    ///     the event would be cleared before it was ever read, so a GLTF loaded into a
    ///     propagated-invisible hierarchy would pop in visible and stay visible until an unrelated dirty
    ///     toggle. Keeping this drain on the every-frame cadence closes that gap; the throttleable
    ///     dirty-driven scans live in <see cref="GltfContainerVisibilitySystem" />.
    ///     </para>
    ///     Ordered after <see cref="GltfContainerVisibilitySystem" /> (which itself runs after the
    ///     producer FinalizeGltfContainerLoadingSystem) so events produced this frame are consumed before
    ///     ClearEntityEventsSystem clears the buffer.
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(GltfContainerVisibilitySystem))]
    public partial class GltfContainerVisibilityEventSystem : VisibilitySystemBase<GltfContainerComponent>
    {
        internal GltfContainerVisibilityEventSystem(World world, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world, eventsBuffer)
        {
        }

        protected override void Update(float t)
        {
            ApplyNewlyCreatedRenderables();
        }

        protected override void UpdateVisibilityInternal(in GltfContainerComponent component, bool visible)
        {
            // we have several states that are notified with events
            if (component.State != LoadingState.Finished) return;

            component.Promise.Result!.Value.Asset!.SetRenderersActive(visible);
        }
    }
}
