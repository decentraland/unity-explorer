using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using ECS.Unity.Visibility.Components;
using System.Collections.Generic;

namespace ECS.Unity.Visibility.Systems
{
    /// <summary>
    /// Propagates visibility from parent entities to children when PropagateToChildren is enabled.
    /// Runs before the visibility application systems to compute the resolved visibility state.
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    [UpdateBefore(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class VisibilityPropagationSystem : BaseUnityLoopSystem
    {
        // Cache to avoid allocations during hierarchy traversal
        private readonly Stack<Entity> childrenStack = new(32);

        public VisibilityPropagationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            // Step 1: Handle entities with their own PBVisibilityComponent that is dirty
            // This creates/updates ResolvedVisibilityComponent for entities with their own visibility
            HandleOwnVisibilityDirtyQuery(World);

            // Step 2: Propagate visibility to children when PropagateToChildren is set
            PropagateToChildrenQuery(World);

            // Step 3: Handle reparenting - only when SDKTransform.IsDirty
            HandleReparentingQuery(World);

            // Step 4: Handle removal of PBVisibilityComponent from entity itself
            HandleOwnVisibilityRemovalQuery(World);

            // Step 5: Handle removal of PBVisibilityComponent from ancestor (children need to reset)
            HandleAncestorVisibilityRemovalQuery(World);
        }

        /// <summary>
        /// For entities with their own PBVisibilityComponent, their own takes priority.
        /// Creates or updates ResolvedVisibilityComponent to match.
        /// </summary>
        [Query]
        private void HandleOwnVisibilityDirty(
            in Entity entity,
            in PBVisibilityComponent visibility,
            in TransformComponent transformComponent)
        {
            if (!visibility.IsDirty) return;

            bool isVisible = visibility.GetVisible();
            bool shouldPropagate = visibility.GetPropagateToChildren();

            ref ResolvedVisibilityComponent resolved = ref World!.TryGetRef<ResolvedVisibilityComponent>(entity, out bool hasResolved);

            if (!hasResolved)
            {
                World.Add(entity, new ResolvedVisibilityComponent
                {
                    IsVisible = isVisible,
                    SourceEntity = entity, // Source is self
                    ShouldPropagate = shouldPropagate,
                    LastKnownParent = transformComponent.Parent,
                    IsDirty = true
                });
            }
            else
            {
                resolved.IsVisible = isVisible;
                resolved.SourceEntity = entity;
                resolved.ShouldPropagate = shouldPropagate;
                resolved.LastKnownParent = transformComponent.Parent;
                resolved.IsDirty = true;
            }
        }

        /// <summary>
        /// When a parent has PropagateToChildren=true and visibility is dirty,
        /// propagate to all descendants that don't have their own PBVisibilityComponent.
        /// </summary>
        [Query]
        private void PropagateToChildren(
            in Entity entity,
            in PBVisibilityComponent visibility,
            in TransformComponent transformComponent)
        {
            // Only propagate if flag is set and component changed
            if (!visibility.IsDirty) return;
            if (!visibility.GetPropagateToChildren()) return;

            bool parentVisible = visibility.GetVisible();

            // Use iterative approach to avoid deep recursion
            PropagateToDescendants(entity, parentVisible, shouldPropagate: true);
        }

        /// <summary>
        /// Detect reparenting by comparing LastKnownParent with current TransformComponent.Parent.
        /// Only check when SDKTransform.IsDirty to avoid per-frame overhead.
        /// </summary>
        [Query]
        [None(typeof(PBVisibilityComponent))] // Skip entities with own visibility - they don't inherit
        private void HandleReparenting(
            in Entity entity,
            ref ResolvedVisibilityComponent resolved,
            in TransformComponent transformComponent,
            in SDKTransform sdkTransform)
        {
            // Only check when transform changed
            if (!sdkTransform.IsDirty) return;

            // Check if parent actually changed (IsDirty could be position/rotation/scale)
            if (resolved.LastKnownParent == transformComponent.Parent) return;

            Entity oldParent = resolved.LastKnownParent;
            resolved.LastKnownParent = transformComponent.Parent;

            // Check new parent's ResolvedVisibility
            if (World!.TryGet(transformComponent.Parent, out ResolvedVisibilityComponent parentResolved)
                && parentResolved.ShouldPropagate)
            {
                // Inherit from new parent
                resolved.IsVisible = parentResolved.IsVisible;
                resolved.SourceEntity = parentResolved.SourceEntity;
                resolved.ShouldPropagate = true;
            }
            else
            {
                // Reset to visible
                resolved.IsVisible = true;
                resolved.SourceEntity = Entity.Null;
                resolved.ShouldPropagate = false;
            }

            resolved.IsDirty = true;

            // Cascade to descendants that don't have their own visibility
            PropagateToDescendants(entity, resolved.IsVisible, resolved.ShouldPropagate);
        }

        /// <summary>
        /// When an entity loses its own PBVisibilityComponent, it should inherit from parent hierarchy.
        /// </summary>
        [Query]
        [All(typeof(ResolvedVisibilityComponent), typeof(RemovedComponents), typeof(TransformComponent))]
        [None(typeof(PBVisibilityComponent))]
        private void HandleOwnVisibilityRemoval(
            in Entity entity,
            ref ResolvedVisibilityComponent resolved,
            ref RemovedComponents removedComponents,
            in TransformComponent transformComponent)
        {
            if (!removedComponents.Remove<PBVisibilityComponent>()) return;

            // Entity lost its own visibility component - recompute from parent
            resolved.LastKnownParent = transformComponent.Parent;

            if (World!.TryGet(transformComponent.Parent, out ResolvedVisibilityComponent parentResolved)
                && parentResolved.ShouldPropagate)
            {
                // Inherit from parent
                resolved.IsVisible = parentResolved.IsVisible;
                resolved.SourceEntity = parentResolved.SourceEntity;
                resolved.ShouldPropagate = true;
            }
            else
            {
                // Reset to visible
                resolved.IsVisible = true;
                resolved.SourceEntity = Entity.Null;
                resolved.ShouldPropagate = false;
            }

            resolved.IsDirty = true;

            // Cascade to descendants
            PropagateToDescendants(entity, resolved.IsVisible, resolved.ShouldPropagate);
        }

        /// <summary>
        /// When an entity with PropagateToChildren=true loses its PBVisibilityComponent,
        /// all children that were inheriting from it need to reset their visibility.
        /// </summary>
        [Query]
        [All(typeof(RemovedComponents), typeof(TransformComponent))]
        [None(typeof(PBVisibilityComponent))]
        private void HandleAncestorVisibilityRemoval(
            in Entity entity,
            ref RemovedComponents removedComponents,
            in TransformComponent transformComponent)
        {
            if (!removedComponents.Remove<PBVisibilityComponent>()) return;

            // This entity just lost its PBVisibilityComponent
            // Reset all descendants that were sourced from this entity to visible
            ResetDescendantsSourcedFrom(entity, transformComponent);
        }

        /// <summary>
        /// Propagates visibility to all descendants, skipping those with their own PBVisibilityComponent.
        /// </summary>
        private void PropagateToDescendants(Entity sourceEntity, bool visibility, bool shouldPropagate)
        {
            childrenStack.Clear();

            // Get initial children
            if (World!.TryGet(sourceEntity, out TransformComponent transformComponent))
            {
                foreach (Entity child in transformComponent.Children)
                    childrenStack.Push(child);
            }

            while (childrenStack.Count > 0)
            {
                Entity childEntity = childrenStack.Pop();

                if (!World.IsAlive(childEntity)) continue;

                // If child has its own PBVisibilityComponent, it takes priority - skip propagation
                if (World.Has<PBVisibilityComponent>(childEntity))
                    continue;

                // Get child's transform for parent tracking
                if (!World.TryGet(childEntity, out TransformComponent childTransform))
                    continue;

                // Update or create ResolvedVisibilityComponent for this child
                ref ResolvedVisibilityComponent resolved = ref World.TryGetRef<ResolvedVisibilityComponent>(childEntity, out bool hasResolved);

                if (!hasResolved)
                {
                    World.Add(childEntity, new ResolvedVisibilityComponent
                    {
                        IsVisible = visibility,
                        SourceEntity = sourceEntity,
                        ShouldPropagate = shouldPropagate,
                        LastKnownParent = childTransform.Parent,
                        IsDirty = true
                    });
                }
                else
                {
                    resolved.IsVisible = visibility;
                    resolved.SourceEntity = sourceEntity;
                    resolved.ShouldPropagate = shouldPropagate;
                    resolved.LastKnownParent = childTransform.Parent;
                    resolved.IsDirty = true;
                }

                // Continue to this child's children
                foreach (Entity grandchild in childTransform.Children)
                    childrenStack.Push(grandchild);
            }
        }

        /// <summary>
        /// Resets visibility for all descendants that were sourced from the specified entity.
        /// Used when an ancestor loses its PBVisibilityComponent.
        /// </summary>
        private void ResetDescendantsSourcedFrom(Entity removedSourceEntity, in TransformComponent transformComponent)
        {
            childrenStack.Clear();

            foreach (Entity child in transformComponent.Children)
                childrenStack.Push(child);

            while (childrenStack.Count > 0)
            {
                Entity childEntity = childrenStack.Pop();

                if (!World!.IsAlive(childEntity)) continue;

                // If child has its own PBVisibilityComponent, it takes priority - skip
                if (World.Has<PBVisibilityComponent>(childEntity))
                    continue;

                // Only reset if this child was sourced from the removed entity
                ref ResolvedVisibilityComponent resolved = ref World.TryGetRef<ResolvedVisibilityComponent>(childEntity, out bool hasResolved);

                if (!hasResolved) continue;

                if (resolved.SourceEntity != removedSourceEntity) continue;

                // Get child's transform for parent tracking and further iteration
                if (!World.TryGet(childEntity, out TransformComponent childTransform))
                    continue;

                // Check if there's a new parent with propagation
                if (World.TryGet(childTransform.Parent, out ResolvedVisibilityComponent parentResolved)
                    && parentResolved.ShouldPropagate)
                {
                    // Inherit from new parent in hierarchy
                    resolved.IsVisible = parentResolved.IsVisible;
                    resolved.SourceEntity = parentResolved.SourceEntity;
                    resolved.ShouldPropagate = true;
                }
                else
                {
                    // Reset to visible
                    resolved.IsVisible = true;
                    resolved.SourceEntity = Entity.Null;
                    resolved.ShouldPropagate = false;
                }

                resolved.IsDirty = true;

                // Continue to grandchildren
                foreach (Entity grandchild in childTransform.Children)
                    childrenStack.Push(grandchild);
            }
        }
    }
}

