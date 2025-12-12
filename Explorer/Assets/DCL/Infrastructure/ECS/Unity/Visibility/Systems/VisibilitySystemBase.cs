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

        protected override void Update(float t)
        {
            // Primary: use ResolvedVisibilityComponent (handles propagation)
            UpdateVisibilityFromResolvedQuery(World!);

            // Fallback: direct PBVisibilityComponent for entities without resolved visibility
            // (backwards compatibility for entities not yet processed by propagation system)
            UpdateVisibilityFromDirectQuery(World);

            // Handle newly created renderable components
            eventsBuffer.ForEach(forEachEvent);

            // Handle removal
            HandleComponentRemovalQuery(World);
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
        private void UpdateVisibilityFromResolved(in TComponent component, ref ResolvedVisibilityComponent resolved)
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
        private void UpdateVisibilityFromDirect(in TComponent component, in PBVisibilityComponent visibility)
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
