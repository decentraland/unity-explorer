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
            // Handle entities with their own PBVisibilityComponent
            // This creates/updates ResolvedVisibilityComponent for entities with their own visibility
            HandleOwnVisibilityDirtyQuery(World);

            // Propagate visibility to children when PropagateToChildren is set
            PropagateToChildrenQuery(World);

            // Handle reparenting for entities that already have ResolvedVisibilityComponent
            HandleReparentingQuery(World);

            // Handle reparenting for entities that never had ResolvedVisibilityComponent
            // (e.g., entity reparented under a propagating parent for the first time)
            HandleFirstTimeReparentingQuery(World);

            // Handle removal of PBVisibilityComponent from entity itself
            HandleOwnVisibilityRemovalQuery(World);

            // Handle removal of PBVisibilityComponent from ancestor (children need to reset)
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
                    SourceEntity = entity,
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
            if (!visibility.IsDirty) return;
            if (!visibility.GetPropagateToChildren()) return;

            PropagateToDescendants(entity, entity, visibility.GetVisible(), shouldPropagate: true);
        }

        /// <summary>
        /// Handles reparenting for entities that already have ResolvedVisibilityComponent.
        /// Uses SDKTransform.IsDirty to avoid per-frame overhead.
        /// </summary>
        [Query]
        [None(typeof(PBVisibilityComponent))] // Skip entities with own visibility - they don't inherit
        private void HandleReparenting(
            in Entity entity,
            ref ResolvedVisibilityComponent resolved,
            in TransformComponent transformComponent,
            in SDKTransform sdkTransform)
        {
            if (!sdkTransform.IsDirty) return;
            if (resolved.LastKnownParent == transformComponent.Parent) return;

            resolved.LastKnownParent = transformComponent.Parent;

            (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(transformComponent.Parent);

            if (found)
            {
                resolved.IsVisible = visibility;
                resolved.SourceEntity = visibilitySource;
                resolved.ShouldPropagate = true;
            }
            else
            {
                resolved.IsVisible = true;
                resolved.SourceEntity = Entity.Null;
                resolved.ShouldPropagate = false;
            }

            resolved.IsDirty = true;
            PropagateToDescendants(entity, resolved.SourceEntity, resolved.IsVisible, resolved.ShouldPropagate);
        }

        /// <summary>
        /// Handles reparenting for entities that never had ResolvedVisibilityComponent.
        /// Covers the case where an entity gets reparented under a propagating parent for the first time.
        /// </summary>
        [Query]
        [None(typeof(PBVisibilityComponent), typeof(ResolvedVisibilityComponent))]
        private void HandleFirstTimeReparenting(
            in Entity entity,
            in TransformComponent transformComponent,
            in SDKTransform sdkTransform)
        {
            if (!sdkTransform.IsDirty) return;

            (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(transformComponent.Parent);

            if (!found)
                return;

            // Capture children BEFORE adding component - archetype change invalidates the reference
            HashSet<Entity> children = transformComponent.Children;

            World!.Add(entity, new ResolvedVisibilityComponent
            {
                IsVisible = visibility,
                SourceEntity = visibilitySource,
                ShouldPropagate = true,
                LastKnownParent = transformComponent.Parent,
                IsDirty = true
            });

            PropagateToDescendantsFromChildren(children, visibilitySource, visibility, shouldPropagate: true);
        }

        /// <summary>
        /// When an entity loses its PBVisibilityComponent, recomputes visibility from parent hierarchy.
        /// </summary>
        [Query]
        [All(typeof(ResolvedVisibilityComponent), typeof(RemovedComponents), typeof(TransformComponent))]
        [None(typeof(PBVisibilityComponent))]
        private void HandleOwnVisibilityRemoval(
            in Entity entity,
            ref ResolvedVisibilityComponent resolved,
            in RemovedComponents removedComponents,
            in TransformComponent transformComponent)
        {
            if (!removedComponents.Set.Contains(typeof(PBVisibilityComponent))) return;

            resolved.LastKnownParent = transformComponent.Parent;

            (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(transformComponent.Parent);

            if (found)
            {
                resolved.IsVisible = visibility;
                resolved.SourceEntity = visibilitySource;
                resolved.ShouldPropagate = true;
            }
            else
            {
                resolved.IsVisible = true;
                resolved.SourceEntity = Entity.Null;
                resolved.ShouldPropagate = false;
            }

            resolved.IsDirty = true;
            PropagateToDescendants(entity, resolved.SourceEntity, resolved.IsVisible, resolved.ShouldPropagate);
        }

        /// <summary>
        /// When an entity loses its PBVisibilityComponent, resets visibility for descendants that were inheriting from it.
        /// </summary>
        [Query]
        [All(typeof(RemovedComponents), typeof(TransformComponent))]
        [None(typeof(PBVisibilityComponent))]
        private void HandleAncestorVisibilityRemoval(
            in Entity entity,
            in RemovedComponents removedComponents,
            in TransformComponent transformComponent)
        {
            if (!removedComponents.Set.Contains(typeof(PBVisibilityComponent))) return;

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
            if (World!.TryGet(startEntity, out TransformComponent transformComponent))
                PropagateToDescendantsFromChildren(transformComponent.Children, visibilitySource, visibility, shouldPropagate);
        }

        /// <summary>
        /// Propagates visibility to descendants from a given children collection.
        /// Takes children directly to avoid archetype change issues.
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

                if (!World.TryGet(childEntity, out TransformComponent childTransform))
                    continue;

                // Capture before potential archetype change
                HashSet<Entity> grandchildren = childTransform.Children;
                Entity childParent = childTransform.Parent;

                if (World.TryGet(childEntity, out PBVisibilityComponent? childVisibility))
                {
                    // Child has own visibility - if it doesn't propagate, pass through to grandchildren
                    if (!childVisibility!.GetPropagateToChildren())
                    {
                        foreach (Entity grandchild in grandchildren)
                            childrenStack.Push(grandchild);
                    }
                    continue;
                }

                ref ResolvedVisibilityComponent resolved = ref World.TryGetRef<ResolvedVisibilityComponent>(childEntity, out bool hasResolved);

                if (!hasResolved)
                {
                    World.Add(childEntity, new ResolvedVisibilityComponent
                    {
                        IsVisible = visibility,
                        SourceEntity = visibilitySource,
                        ShouldPropagate = shouldPropagate,
                        LastKnownParent = childParent,
                        IsDirty = true
                    });
                }
                else
                {
                    resolved.IsVisible = visibility;
                    resolved.SourceEntity = visibilitySource;
                    resolved.ShouldPropagate = shouldPropagate;
                    resolved.LastKnownParent = childParent;
                    resolved.IsDirty = true;
                }

                foreach (Entity grandchild in grandchildren)
                    childrenStack.Push(grandchild);
            }
        }

        /// <summary>
        /// Resets visibility for descendants that were sourced from the specified entity.
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

                if (!World.TryGet(childEntity, out TransformComponent childTransform))
                    continue;

                if (World.TryGet(childEntity, out PBVisibilityComponent? childVisibility))
                {
                    // Child has own visibility - if it doesn't propagate, pass through to grandchildren
                    if (!childVisibility!.GetPropagateToChildren())
                    {
                        foreach (Entity grandchild in childTransform.Children)
                            childrenStack.Push(grandchild);
                    }
                    continue;
                }

                ref ResolvedVisibilityComponent resolved = ref World.TryGetRef<ResolvedVisibilityComponent>(childEntity, out bool hasResolved);

                if (!hasResolved) continue;
                if (resolved.SourceEntity != removedSourceEntity) continue;

                (Entity visibilitySource, bool visibility, bool found) = FindPropagatingAncestor(childTransform.Parent);

                if (found)
                {
                    resolved.IsVisible = visibility;
                    resolved.SourceEntity = visibilitySource;
                    resolved.ShouldPropagate = true;
                }
                else
                {
                    resolved.IsVisible = true;
                    resolved.SourceEntity = Entity.Null;
                    resolved.ShouldPropagate = false;
                }

                resolved.IsDirty = true;

                foreach (Entity grandchild in childTransform.Children)
                    childrenStack.Push(grandchild);
            }
        }

        /// <summary>
        /// Finds the nearest propagating ancestor, handling the "pass-through" case where
        /// an intermediate ancestor has own visibility with propagateToChildren=FALSE.
        /// </summary>
        private (Entity source, bool visibility, bool found) FindPropagatingAncestor(Entity startParent)
        {
            Entity current = startParent;

            while (World!.IsAlive(current))
            {
                if (!World.TryGet(current, out ResolvedVisibilityComponent resolved))
                    return (Entity.Null, true, false);

                if (resolved.ShouldPropagate)
                    return (resolved.SourceEntity, resolved.IsVisible, true);

                // Check for pass-through: has own visibility but doesn't propagate
                if (World.TryGet(current, out PBVisibilityComponent? visibility))
                {
                    if (visibility!.GetPropagateToChildren())
                        return (current, resolved.IsVisible, true);
                }
                else
                {
                    return (Entity.Null, true, false);
                }

                if (!World.TryGet(current, out TransformComponent transform))
                    return (Entity.Null, true, false);

                current = transform.Parent;
            }

            return (Entity.Null, true, false);
        }
    }
}
