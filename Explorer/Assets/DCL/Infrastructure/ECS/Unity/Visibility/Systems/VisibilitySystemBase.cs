using Arch.Core;
using Arch.System;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Visibility.Components;
using System.Runtime.CompilerServices;

namespace ECS.Unity.Visibility.Systems
{
    public abstract partial class VisibilitySystemBase<TComponent> : BaseUnityLoopSystem
    {
        private readonly EntityEventBuffer<TComponent> eventsBuffer;
        private readonly EntityEventBuffer<TComponent>.ForEachDelegate forEachEvent;

        protected VisibilitySystemBase(World world, EntityEventBuffer<TComponent> eventsBuffer) : base(world)
        {
            this.eventsBuffer = eventsBuffer;
            forEachEvent = ProcessEvent;
        }

        /// <summary>
        ///     Default cadence used by consumers that are not split into throttled/un-throttled halves
        ///     (e.g. text-shape and NFT-shape visibility): dirty scans, then the every-frame event drain,
        ///     then removal — preserving the original ordering and behavior.
        /// </summary>
        protected override void Update(float t)
        {
            UpdateDirtyDrivenVisibility();
            ApplyNewlyCreatedRenderables();
            HandleRemovedVisibilityComponents();
        }

        /// <summary>
        ///     Dirty-driven visibility application (the two full renderer-archetype scans). This is the
        ///     path the split concrete systems carry the <c>[ThrottlingEnabled]</c> attribute for.
        ///     <para>
        ///     Both signals these scans react to can only change while the SDK update gate is open:
        ///     <see cref="ResolvedVisibilityComponent" /><c>.IsDirty</c> is set by the
        ///     <c>[ThrottlingEnabled]</c> <c>VisibilityPropagationSystem</c>, and
        ///     <c>PBVisibilityComponent.IsDirty</c> is set on CRDT application (gate-open) and cleared by
        ///     the <c>[ThrottlingEnabled]</c> <c>ResetDirtyFlagSystem</c>. On gate-closed frames nothing
        ///     could have set <c>IsDirty</c>, so aligning these scans to the producer's throttled cadence
        ///     removes the idle-frame waste with no behavioral change.
        ///     </para>
        /// </summary>
        protected void UpdateDirtyDrivenVisibility()
        {
            // Primary: use ResolvedVisibilityComponent (handles propagation)
            UpdateVisibilityFromResolvedVisibilityQuery(World!);

            // Fallback: direct PBVisibilityComponent for entities without resolved visibility
            // (backwards compatibility for entities not yet processed by propagation system)
            UpdateVisibilityFromPBComponentQuery(World);
        }

        /// <summary>
        ///     Reset-to-visible on visibility-component removal. RemovedComponents is a persistent
        ///     per-entity component (NOT drained by ClearEntityEventsSystem) whose entries are only added
        ///     during CRDT application, so it is safe to co-locate with the throttled dirty scans.
        /// </summary>
        protected void HandleRemovedVisibilityComponents()
        {
            HandleComponentRemovalQuery(World);
        }

        /// <summary>
        ///     Applies visibility to renderables whose create event was produced this frame.
        ///     <para>
        ///     MUST run on every frame: those one-shot events are produced on whatever frame an async load
        ///     resolves (routinely a gate-closed frame for distant/throttled scenes) and are cleared every
        ///     frame by <c>ClearEntityEventsSystem</c>, so a throttled consumer would be skipped and the
        ///     event dropped. For the split systems this runs from the un-throttled companion
        ///     <c>*VisibilityEventSystem</c>.
        ///     </para>
        /// </summary>
        protected void ApplyNewlyCreatedRenderables()
        {
            eventsBuffer.ForEach(forEachEvent);
        }

        /// <summary>
        /// Updates visibility if renderable component was resolved/updated this frame.
        /// Checks ResolvedVisibilityComponent first (supports propagation), then falls back to PBVisibilityComponent.
        /// </summary>
        private void ProcessEvent(Entity entity, TComponent @event)
        {
            // First check ResolvedVisibilityComponent (supports propagation)
            if (World.TryGet(entity, out ResolvedVisibilityComponent resolved))
            {
                UpdateVisibilityInternal(in @event, resolved.IsVisible);
                return;
            }

            // Fallback to direct PBVisibilityComponent
            if (World.TryGet(entity, out PBVisibilityComponent? visibilityComponent))
                UpdateVisibilityInternal(in @event, visibilityComponent!.GetVisible());
        }

        /// <summary>
        /// Updates visibility based on ResolvedVisibilityComponent (supports propagation).
        /// </summary>
        [Query]
        private void UpdateVisibilityFromResolvedVisibility(in TComponent component, ref ResolvedVisibilityComponent resolved)
        {
            if (resolved.IsDirty)
                UpdateVisibilityInternal(in component, resolved.IsVisible);
        }

        /// <summary>
        /// Updates visibility based on PBVisibilityComponent for entities without ResolvedVisibility.
        /// This provides backwards compatibility for entities that haven't been processed by propagation system.
        /// </summary>
        [Query]
        [None(typeof(ResolvedVisibilityComponent))]
        private void UpdateVisibilityFromPBComponent(in TComponent component, in PBVisibilityComponent visibility)
        {
            if (visibility.IsDirty)
                UpdateVisibilityInternal(in component, visibility.GetVisible());
        }

        /// <summary>
        /// Handles removal of visibility components - reset to visible.
        /// </summary>
        [Query]
        [None(typeof(PBVisibilityComponent), typeof(ResolvedVisibilityComponent))]
        private void HandleComponentRemoval(ref RemovedComponents removedComponents, ref TComponent rendererComponent)
        {
            // Reset to visible if visibility-related components are removed
            if (removedComponents.Set.Contains(typeof(PBVisibilityComponent)) ||
                removedComponents.Set.Contains(typeof(ResolvedVisibilityComponent)))
                UpdateVisibilityInternal(in rendererComponent, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void UpdateVisibilityInternal(in TComponent @event, bool visible);
    }
}
