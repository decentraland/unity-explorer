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

            // Step 3: Handle reparenting for entities that already have ResolvedVisibilityComponent
            HandleReparentingQuery(World);

            // Step 4: Handle reparenting for entities that never had ResolvedVisibilityComponent
            // (e.g., entity reparented under a propagating parent for the first time)
            HandleFirstTimeReparentingQuery(World);

            // Step 5: Handle removal of PBVisibilityComponent from entity itself
            HandleOwnVisibilityRemovalQuery(World);

            // Step 6: Handle removal of PBVisibilityComponent from ancestor (children need to reset)
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
            // entity is both the start point and the visibility source (it has PBVisibilityComponent)
            PropagateToDescendants(startEntity: entity, visibilitySource: entity, parentVisible, shouldPropagate: true);
        }

        /// <summary>
        /// Detect reparenting by comparing LastKnownParent with current TransformComponent.Parent.
        /// Only check when SDKTransform.IsDirty to avoid per-frame overhead.
        /// This handles entities that already have ResolvedVisibilityComponent.
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

            // Find propagating ancestor (handles pass-through case where immediate parent
            // has own visibility with propagateToChildren=FALSE but is inside a propagating hierarchy)
            (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(transformComponent.Parent);

            if (found)
            {
                // Inherit from propagating ancestor
                resolved.IsVisible = visibility;
                resolved.SourceEntity = visibilitySource;
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
            // entity is the start point, but the visibility source is resolved.SourceEntity
            PropagateToDescendants(startEntity: entity, visibilitySource: resolved.SourceEntity, resolved.IsVisible, resolved.ShouldPropagate);
        }

        /// <summary>
        /// Handle reparenting for entities that never had ResolvedVisibilityComponent before.
        /// This covers the case where an entity that was never under a propagating parent
        /// gets reparented under a parent that has propagating visibility.
        /// </summary>
        [Query]
        [None(typeof(PBVisibilityComponent), typeof(ResolvedVisibilityComponent))]
        private void HandleFirstTimeReparenting(
            in Entity entity,
            in TransformComponent transformComponent,
            in SDKTransform sdkTransform)
        {
            // Only check when transform changed
            if (!sdkTransform.IsDirty) return;

            // Find propagating ancestor (handles pass-through case where immediate parent
            // has own visibility with propagateToChildren=FALSE but is inside a propagating hierarchy)
            (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(transformComponent.Parent);

            if (!found)
                return;

            // IMPORTANT: Capture children BEFORE adding component, as adding component changes archetype
            // and the transformComponent reference might become stale
            HashSet<Entity> children = transformComponent.Children;

            // Create ResolvedVisibilityComponent inheriting from propagating ancestor
            World!.Add(entity, new ResolvedVisibilityComponent
            {
                IsVisible = visibility,
                SourceEntity = visibilitySource,
                ShouldPropagate = true,
                LastKnownParent = transformComponent.Parent,
                IsDirty = true
            });

            // Cascade to descendants that don't have their own visibility
            // Pass children directly to avoid issues with archetype changes
            PropagateToDescendantsFromChildren(children, visibilitySource: visibilitySource, visibility, shouldPropagate: true);
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

            // Entity lost its own visibility component - recompute from parent hierarchy
            resolved.LastKnownParent = transformComponent.Parent;

            // Find propagating ancestor (handles pass-through case)
            (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(transformComponent.Parent);

            if (found)
            {
                // Inherit from propagating ancestor
                resolved.IsVisible = visibility;
                resolved.SourceEntity = visibilitySource;
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
            // entity is the start point, but the visibility source is resolved.SourceEntity
            PropagateToDescendants(startEntity: entity, visibilitySource: resolved.SourceEntity, resolved.IsVisible, resolved.ShouldPropagate);
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
        /// <param name="startEntity">The entity whose children to start iterating from</param>
        /// <param name="visibilitySource">The original entity that has PBVisibilityComponent (used as SourceEntity)</param>
        /// <param name="visibility">The visibility value to propagate</param>
        /// <param name="shouldPropagate">Whether this visibility should continue to propagate</param>
        private void PropagateToDescendants(Entity startEntity, Entity visibilitySource, bool visibility, bool shouldPropagate)
        {
            // Get initial children from the start entity
            if (World!.TryGet(startEntity, out TransformComponent transformComponent))
            {
                PropagateToDescendantsFromChildren(transformComponent.Children, visibilitySource, visibility, shouldPropagate);
            }
        }

        /// <summary>
        /// Propagates visibility to all descendants starting from a given children collection.
        /// Use this when you already have the children and want to avoid potential issues with archetype changes.
        /// </summary>
        private void PropagateToDescendantsFromChildren(IEnumerable<Entity> initialChildren, Entity visibilitySource, bool visibility, bool shouldPropagate)
        {
            childrenStack.Clear();

            foreach (Entity child in initialChildren)
                childrenStack.Push(child);

            while (childrenStack.Count > 0)
            {
                Entity childEntity = childrenStack.Pop();

                if (!World!.IsAlive(childEntity)) continue;

                // Get child's transform for parent tracking
                if (!World.TryGet(childEntity, out TransformComponent childTransform))
                    continue;

                // Capture children before potentially adding component (archetype change)
                HashSet<Entity> grandchildren = childTransform.Children;
                Entity childParent = childTransform.Parent;

                // If child has its own PBVisibilityComponent, check its propagation behavior
                if (World.TryGet(childEntity, out PBVisibilityComponent? childVisibility))
                {
                    // If child propagates its own visibility, it takes over - don't continue to grandchildren
                    // If child does NOT propagate, we "pass through" to grandchildren with ancestor's visibility
                    if (!childVisibility!.GetPropagateToChildren())
                    {
                        // Pass-through: continue to grandchildren with the original visibility source
                        foreach (Entity grandchild in grandchildren)
                            childrenStack.Push(grandchild);
                    }
                    // Either way, don't update this child's visibility - it has its own
                    continue;
                }

                // Update or create ResolvedVisibilityComponent for this child
                ref ResolvedVisibilityComponent resolved = ref World.TryGetRef<ResolvedVisibilityComponent>(childEntity, out bool hasResolved);

                if (!hasResolved)
                {
                    World.Add(childEntity, new ResolvedVisibilityComponent
                    {
                        IsVisible = visibility,
                        SourceEntity = visibilitySource, // Use the original visibility source
                        ShouldPropagate = shouldPropagate,
                        LastKnownParent = childParent,
                        IsDirty = true
                    });
                }
                else
                {
                    resolved.IsVisible = visibility;
                    resolved.SourceEntity = visibilitySource; // Use the original visibility source
                    resolved.ShouldPropagate = shouldPropagate;
                    resolved.LastKnownParent = childParent;
                    resolved.IsDirty = true;
                }

                // Continue to this child's children (using captured reference)
                foreach (Entity grandchild in grandchildren)
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

                // Get child's transform for parent tracking and further iteration
                if (!World.TryGet(childEntity, out TransformComponent childTransform))
                    continue;

                // If child has its own PBVisibilityComponent, check its propagation behavior
                if (World.TryGet(childEntity, out PBVisibilityComponent? childVisibility))
                {
                    // If child propagates its own visibility, it handles its descendants - skip
                    // If child does NOT propagate, we need to "pass through" to reset grandchildren
                    if (!childVisibility!.GetPropagateToChildren())
                    {
                        // Pass-through: continue to grandchildren
                        foreach (Entity grandchild in childTransform.Children)
                            childrenStack.Push(grandchild);
                    }
                    // Either way, don't reset this child's visibility - it has its own
                    continue;
                }

                // Only reset if this child was sourced from the removed entity
                ref ResolvedVisibilityComponent resolved = ref World.TryGetRef<ResolvedVisibilityComponent>(childEntity, out bool hasResolved);

                if (!hasResolved) continue;

                if (resolved.SourceEntity != removedSourceEntity) continue;

                // Find propagating ancestor (handles pass-through case)
                (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(childTransform.Parent);

                if (found)
                {
                    // Inherit from propagating ancestor in hierarchy
                    resolved.IsVisible = visibility;
                    resolved.SourceEntity = visibilitySource;
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

        /// <summary>
        /// Walks up the hierarchy from startParent to find the nearest propagating ancestor.
        /// Handles the "pass-through" case where an intermediate ancestor has its own visibility
        /// with propagateToChildren=FALSE but is inside a propagating hierarchy.
        /// </summary>
        /// <param name="startParent">The parent entity to start searching from</param>
        /// <returns>
        /// A tuple containing:
        /// - source: The entity that is the source of the propagated visibility
        /// - visibility: The visibility value to inherit
        /// - found: Whether a propagating ancestor was found
        /// </returns>
        private (Entity source, bool visibility, bool found) FindPropagatingAncestor(Entity startParent)
        {
            Entity current = startParent;

            while (World!.IsAlive(current))
            {
                // Check if current entity has ResolvedVisibilityComponent
                if (!World.TryGet(current, out ResolvedVisibilityComponent resolved))
                    return (Entity.Null, true, false);

                // If current entity propagates, we found our source
                if (resolved.ShouldPropagate)
                    return (resolved.SourceEntity, resolved.IsVisible, true);

                // Current doesn't propagate - check if it's a "pass-through" case
                // (has own visibility with propagateToChildren=FALSE)
                if (World.TryGet(current, out PBVisibilityComponent? visibility))
                {
                    if (visibility!.GetPropagateToChildren())
                    {
                        // This entity propagates its own visibility - use it
                        return (current, resolved.IsVisible, true);
                    }
                    // else: Has own visibility but doesn't propagate - continue up the hierarchy
                }
                else
                {
                    // Has ResolvedVisibility but no PBVisibility and doesn't propagate
                    // This shouldn't happen in normal cases, stop searching
                    return (Entity.Null, true, false);
                }

                // Move to parent
                if (!World.TryGet(current, out TransformComponent transform))
                    return (Entity.Null, true, false);

                current = transform.Parent;
            }

            return (Entity.Null, true, false);
        }
    }
}

