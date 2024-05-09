using Arch.Core;
using Arch.System;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.LifeCycle.Components;
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
            UpdateVisibilityQuery(World!);
            eventsBuffer.ForEach(forEachEvent);
            HandleComponentRemovalQuery(World);
        }

        /// <summary>
        /// Updates visibility if text component was resolved/updated this frame
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="event"></param>
        private void ProcessEvent(Entity entity, TComponent @event)
        {
            if (World.TryGet(entity, out PBVisibilityComponent? visibilityComponent))
                UpdateVisibilityInternal(@event, visibilityComponent!.GetVisible());
        }

        /// <summary>
        ///     Updates visibility based on PBVisibilityComponent isDirty
        /// </summary>
        [Query]
        private void UpdateVisibility(in TComponent component, in PBVisibilityComponent visibility)
        {
            if (visibility.IsDirty)
                UpdateVisibilityInternal(in component, visibility.GetVisible());
        }

        [Query]
        [None(typeof(PBVisibilityComponent))]
        private void HandleComponentRemoval(ref RemovedComponents removedComponents, ref TComponent primitiveMeshRendererComponent)
        {
            if (removedComponents.Remove<PBVisibilityComponent>())
                UpdateVisibilityInternal(in primitiveMeshRendererComponent, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void UpdateVisibilityInternal(in TComponent @event, bool visible);
    }
}
