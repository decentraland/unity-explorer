using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Visibility.Components;
using ECS.Unity.Visibility.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Visibility.Tests
{
    [TestFixture]
    public class VisibilityPropagationSystemShould : UnitySystemTestBase<VisibilityPropagationSystem>
    {
        private readonly List<GameObject> createdGameObjects = new();

        [SetUp]
        public void SetUp()
        {
            system = new VisibilityPropagationSystem(world);
        }

        protected override void OnTearDown()
        {
            foreach (GameObject go in createdGameObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            createdGameObjects.Clear();
        }

        private TransformComponent CreateTransformComponent(string name)
        {
            var go = new GameObject(name);
            createdGameObjects.Add(go);
            return new TransformComponent(go.transform);
        }

        private void SetupParentChild(Entity parent, Entity child, TransformComponent parentTransform, TransformComponent childTransform)
        {
            childTransform.Transform.SetParent(parentTransform.Transform, true);
            childTransform.Parent = parent;
            parentTransform.Children.Add(child);
        }

        #region Scenario 1: Child with own VisibilityComponent takes priority

        [Test]
        public void ChildWithOwnVisibilityComponent_TakesPriority()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                new PBVisibilityComponent { Visible = true, IsDirty = true }, // Child's own visibility = true
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);

            // Act
            system!.Update(0f);

            // Assert
            var childResolved = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolved.IsVisible, Is.True, "Child's own visibility should take priority");
            Assert.That(childResolved.SourceEntity, Is.EqualTo(child), "Source should be child itself");
        }

        [Test]
        public void PropagationPassesThroughChildWithNonPropagatingVisibility()
        {
            // Scenario: Parent propagates visibility, but intermediate child has its own visibility state
            // with propagateToChildren=FALSE. The grandchild should inherit from the grandparent
            // (passing through the intermediate child).
            //
            // EntityA (parent): visible=FALSE, propagateToChildren=TRUE
            // EntityB (child): visible=TRUE, propagateToChildren=FALSE → B is visible, but doesn't propagate its own visibility state
            // EntityC (grandchild): should inherit from EntityA (visible=FALSE)

            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");
            var grandchildTransform = CreateTransformComponent("Grandchild");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                new PBVisibilityComponent { Visible = true, PropagateToChildren = false, IsDirty = true },
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity grandchild = world.Create(
                grandchildTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);
            SetupParentChild(child, grandchild, childTransform, grandchildTransform);

            // Act
            system!.Update(0f);

            // Assert - child uses its own visibility state (true), but grandchild inherits from grandparent (false)
            var childResolved = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolved.IsVisible, Is.True, "Child should use its own visibility (true)");
            Assert.That(childResolved.SourceEntity, Is.EqualTo(child), "Child's source should be itself");

            Assert.That(world.Has<ResolvedVisibilityComponent>(grandchild), Is.True,
                "Grandchild should have ResolvedVisibilityComponent (pass-through from grandparent)");
            var grandchildResolved = world.Get<ResolvedVisibilityComponent>(grandchild);
            Assert.That(grandchildResolved.IsVisible, Is.False, "Grandchild should inherit grandparent's visibility (false)");
            Assert.That(grandchildResolved.SourceEntity, Is.EqualTo(parent), "Grandchild's source should be grandparent");
        }

        [Test]
        public void PropagationBlockedByChildWithPropagatingVisibility()
        {
            // Contrast test to PropagationPassesThroughChildWithNonPropagatingVisibility:
            // When intermediate child has propagateToChildren=TRUE, its visibility takes over
            // for grandchildren, blocking the grandparent's propagation.
            //
            // EntityA (parent): visible=FALSE, propagateToChildren=TRUE
            // EntityB (child): visible=TRUE, propagateToChildren=TRUE → B propagates its own visibility state
            // EntityC (grandchild): should inherit from EntityB (visible=TRUE), NOT from EntityA

            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");
            var grandchildTransform = CreateTransformComponent("Grandchild");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                new PBVisibilityComponent { Visible = true, PropagateToChildren = true, IsDirty = true },
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity grandchild = world.Create(
                grandchildTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);
            SetupParentChild(child, grandchild, childTransform, grandchildTransform);

            // Act
            system!.Update(0f);

            // Assert - grandchild should inherit from child (visible=TRUE), not grandparent (visible=FALSE)
            var grandchildResolved = world.Get<ResolvedVisibilityComponent>(grandchild);
            Assert.That(grandchildResolved.IsVisible, Is.True, "Grandchild should inherit child's visibility (true)");
            Assert.That(grandchildResolved.SourceEntity, Is.EqualTo(child), "Grandchild's source should be child (which propagates)");
        }

        #endregion

        #region Scenario 2: Child's VisibilityComponent removed - inherits from parent

        [Test]
        public void ChildVisibilityRemoved_InheritsFromParent()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                new PBVisibilityComponent { Visible = true, IsDirty = true },
                childTransform,
                new SDKTransform { IsDirty = false },
                RemovedComponents.CreateDefault()
            );

            SetupParentChild(parent, child, parentTransform, childTransform);

            // First update - both have their own visibility
            system!.Update(0f);

            var childResolvedBefore = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedBefore.IsVisible, Is.True, "Child should be visible before removal");

            // Remove child's visibility component
            world.Remove<PBVisibilityComponent>(child);
            ref var removedComponents = ref world.Get<RemovedComponents>(child);
            removedComponents.Remove<PBVisibilityComponent>();

            // Act
            system.Update(0f);

            // Assert
            var childResolvedAfter = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedAfter.IsVisible, Is.False, "Child should inherit parent's visibility (false) after removal");
            Assert.That(childResolvedAfter.SourceEntity, Is.EqualTo(parent), "Source should be parent after removal");
        }

        #endregion

        #region Scenario 3: Child with own VisibilityComponent + propagateToChildren

        [Test]
        public void ChildWithPropagateToChildren_PropagatesItsOwnVisibility()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");
            var grandchildTransform = CreateTransformComponent("Grandchild");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                new PBVisibilityComponent { Visible = true, PropagateToChildren = true, IsDirty = true },
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity grandchild = world.Create(
                grandchildTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);
            SetupParentChild(child, grandchild, childTransform, grandchildTransform);

            // Act
            system!.Update(0f);

            // Assert
            var grandchildResolved = world.Get<ResolvedVisibilityComponent>(grandchild);
            Assert.That(grandchildResolved.IsVisible, Is.True, "Grandchild should inherit child's visibility (true), not parent's (false)");
            Assert.That(grandchildResolved.SourceEntity, Is.EqualTo(child), "Source should be child, not parent");
        }

        #endregion

        #region Scenario 4: Reparenting

        [Test]
        public void ReparentedToNonPropagatingHierarchy_ResetsToVisible()
        {
            // Arrange
            var oldParentTransform = CreateTransformComponent("OldParent");
            var newParentTransform = CreateTransformComponent("NewParent");
            var childTransform = CreateTransformComponent("Child");

            Entity oldParent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                oldParentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity newParent = world.Create(
                newParentTransform, // No visibility component
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(oldParent, child, oldParentTransform, childTransform);

            // First update - child inherits old parent's visibility
            system!.Update(0f);

            var childResolvedBefore = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedBefore.IsVisible, Is.False, "Child should inherit old parent's visibility");

            // Reparent child to new parent
            oldParentTransform.Children.Remove(child);
            childTransform.Transform.SetParent(newParentTransform.Transform, true);
            childTransform.Parent = newParent;
            newParentTransform.Children.Add(child);

            // Mark transform as dirty to trigger reparenting detection
            ref var childSdkTransform = ref world.Get<SDKTransform>(child);
            childSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert
            var childResolvedAfter = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedAfter.IsVisible, Is.True, "Child should reset to visible after reparenting to non-propagating hierarchy");
            Assert.That(childResolvedAfter.ShouldPropagate, Is.False, "ShouldPropagate should be false");
        }

        [Test]
        public void ReparentedToPropagatingHierarchy_InheritsNewParentVisibility()
        {
            // Arrange
            var oldParentTransform = CreateTransformComponent("OldParent");
            var newParentTransform = CreateTransformComponent("NewParent");
            var childTransform = CreateTransformComponent("Child");

            Entity oldParent = world.Create(
                oldParentTransform, // No visibility component
                new SDKTransform { IsDirty = false }
            );

            Entity newParent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                newParentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(oldParent, child, oldParentTransform, childTransform);

            // First update - process new parent's visibility
            system!.Update(0f);

            // Reparent child to new parent (which has propagating visibility)
            oldParentTransform.Children.Remove(child);
            childTransform.Transform.SetParent(newParentTransform.Transform, true);
            childTransform.Parent = newParent;
            newParentTransform.Children.Add(child);

            // Add ResolvedVisibility to child (simulating it had one before)
            world.Add(child, new ResolvedVisibilityComponent
            {
                IsVisible = true,
                SourceEntity = Entity.Null,
                ShouldPropagate = false,
                LastKnownParent = oldParent,
                IsDirty = false
            });

            // Mark transform as dirty to trigger reparenting detection
            ref var childSdkTransform = ref world.Get<SDKTransform>(child);
            childSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert
            var childResolvedAfter = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedAfter.IsVisible, Is.False, "Child should inherit new parent's visibility (false)");
            Assert.That(childResolvedAfter.SourceEntity, Is.EqualTo(newParent), "Source should be new parent");
            Assert.That(childResolvedAfter.ShouldPropagate, Is.True, "ShouldPropagate should be true");
        }

        [Test]
        public void ReparentedToPropagatingHierarchy_FirstTimeWithoutResolvedVisibility()
        {
            // This tests the case where an entity that NEVER had ResolvedVisibilityComponent
            // (because it was never under a propagating parent) gets reparented under a
            // parent that has propagating visibility.

            // Arrange
            var oldParentTransform = CreateTransformComponent("OldParent");
            var newParentTransform = CreateTransformComponent("NewParent");
            var childTransform = CreateTransformComponent("Child");

            Entity oldParent = world.Create(
                oldParentTransform, // No visibility component at all
                new SDKTransform { IsDirty = false }
            );

            Entity newParent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                newParentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(oldParent, child, oldParentTransform, childTransform);

            // First update - new parent gets its ResolvedVisibility, but child has none
            system!.Update(0f);

            // Verify child has NO ResolvedVisibilityComponent yet
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.False,
                "Child should NOT have ResolvedVisibilityComponent before reparenting");

            // Reparent child to new parent (which has propagating visibility)
            oldParentTransform.Children.Remove(child);
            childTransform.Transform.SetParent(newParentTransform.Transform, true);
            childTransform.Parent = newParent;
            newParentTransform.Children.Add(child);

            // Mark transform as dirty to trigger reparenting detection
            ref var childSdkTransform = ref world.Get<SDKTransform>(child);
            childSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert - child should now have ResolvedVisibilityComponent inheriting from new parent
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.True,
                "Child should have ResolvedVisibilityComponent after reparenting to propagating parent");

            var childResolved = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolved.IsVisible, Is.False, "Child should inherit new parent's visibility (false)");
            Assert.That(childResolved.SourceEntity, Is.EqualTo(newParent), "Source should be new parent");
            Assert.That(childResolved.ShouldPropagate, Is.True, "ShouldPropagate should be true");
        }

        [Test]
        public void ReparentedToPropagatingHierarchy_CascadesToGrandchildren()
        {
            // This tests that when an entity is reparented under a propagating parent,
            // the visibility also propagates to all descendants (children of the reparented entity).

            // Arrange
            var oldParentTransform = CreateTransformComponent("OldParent");
            var newParentTransform = CreateTransformComponent("NewParent");
            var childTransform = CreateTransformComponent("Child");
            var grandchildTransform = CreateTransformComponent("Grandchild");

            Entity oldParent = world.Create(
                oldParentTransform, // No visibility component
                new SDKTransform { IsDirty = false }
            );

            Entity newParent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                newParentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity grandchild = world.Create(
                grandchildTransform,
                new SDKTransform { IsDirty = false }
            );

            // Set up hierarchy: oldParent -> child -> grandchild
            SetupParentChild(oldParent, child, oldParentTransform, childTransform);
            SetupParentChild(child, grandchild, childTransform, grandchildTransform);

            // First update - newParent gets its ResolvedVisibility
            system!.Update(0f);

            // Verify neither child nor grandchild have ResolvedVisibilityComponent
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.False);
            Assert.That(world.Has<ResolvedVisibilityComponent>(grandchild), Is.False);

            // Reparent child (and its descendants) to new parent
            oldParentTransform.Children.Remove(child);
            childTransform.Transform.SetParent(newParentTransform.Transform, true);
            childTransform.Parent = newParent;
            newParentTransform.Children.Add(child);

            // Mark child's transform as dirty to trigger reparenting detection
            ref var childSdkTransform = ref world.Get<SDKTransform>(child);
            childSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert - both child AND grandchild should have inherited visibility
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.True,
                "Child should have ResolvedVisibilityComponent");
            Assert.That(world.Has<ResolvedVisibilityComponent>(grandchild), Is.True,
                "Grandchild should also have ResolvedVisibilityComponent (propagated through reparenting)");

            var childResolved = world.Get<ResolvedVisibilityComponent>(child);
            var grandchildResolved = world.Get<ResolvedVisibilityComponent>(grandchild);

            Assert.That(childResolved.IsVisible, Is.False, "Child should inherit visibility (false)");
            Assert.That(grandchildResolved.IsVisible, Is.False, "Grandchild should inherit visibility (false)");

            // Both should have the same source (newParent, which has the PBVisibilityComponent)
            Assert.That(childResolved.SourceEntity, Is.EqualTo(newParent), "Child source should be newParent");
            Assert.That(grandchildResolved.SourceEntity, Is.EqualTo(newParent), "Grandchild source should be newParent");
        }

        [Test]
        public void ReparentedUnderPassThroughParent_InheritsFromGrandparent()
        {
            // This tests the pass-through reparenting case:
            // - GrandparentEntity: visible=FALSE, propagate=TRUE
            // - ParentEntity (pass-through): visible=TRUE, propagate=FALSE
            // - ReparentedEntity: reparented under ParentEntity, should inherit from GrandparentEntity (invisible)

            // Arrange
            var grandparentTransform = CreateTransformComponent("Grandparent");
            var parentTransform = CreateTransformComponent("Parent");
            var oldParentTransform = CreateTransformComponent("OldParent");
            var reparentedTransform = CreateTransformComponent("Reparented");

            Entity grandparent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                grandparentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = true, PropagateToChildren = false, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity oldParent = world.Create(
                oldParentTransform, // No visibility component
                new SDKTransform { IsDirty = false }
            );

            Entity reparented = world.Create(
                reparentedTransform,
                new SDKTransform { IsDirty = false }
            );

            // Set up hierarchy: grandparent -> parent (pass-through), oldParent -> reparented
            SetupParentChild(grandparent, parent, grandparentTransform, parentTransform);
            SetupParentChild(oldParent, reparented, oldParentTransform, reparentedTransform);

            // First update - process visibility hierarchy
            system!.Update(0f);

            // Verify parent is visible (has its own visibility state)
            var parentResolved = world.Get<ResolvedVisibilityComponent>(parent);
            Assert.That(parentResolved.IsVisible, Is.True, "Parent should be visible (own visibility)");
            Assert.That(parentResolved.ShouldPropagate, Is.False, "Parent should NOT propagate (propagate=false)");

            // Verify reparented entity has no ResolvedVisibilityComponent yet
            Assert.That(world.Has<ResolvedVisibilityComponent>(reparented), Is.False,
                "Reparented entity should not have ResolvedVisibilityComponent before reparenting");

            // Now reparent the entity under the pass-through parent
            oldParentTransform.Children.Remove(reparented);
            reparentedTransform.Transform.SetParent(parentTransform.Transform, true);
            reparentedTransform.Parent = parent;
            parentTransform.Children.Add(reparented);

            // Mark transform as dirty to trigger reparenting detection
            ref var reparentedSdkTransform = ref world.Get<SDKTransform>(reparented);
            reparentedSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert - reparented entity should inherit from GRANDPARENT (via pass-through)
            Assert.That(world.Has<ResolvedVisibilityComponent>(reparented), Is.True,
                "Reparented entity should have ResolvedVisibilityComponent");

            var reparentedResolved = world.Get<ResolvedVisibilityComponent>(reparented);
            Assert.That(reparentedResolved.IsVisible, Is.False,
                "Reparented entity should inherit grandparent's visibility (false), not parent's (true)");
            Assert.That(reparentedResolved.SourceEntity, Is.EqualTo(grandparent),
                "Reparented entity's source should be grandparent (the propagating ancestor)");
            Assert.That(reparentedResolved.ShouldPropagate, Is.True,
                "Reparented entity should propagate (inheriting the propagation chain)");
        }

        #endregion

        #region Scenario 5: Parent's PBVisibilityComponent removed

        [Test]
        public void ParentVisibilityRemoved_ChildrenResetToVisible()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false },
                RemovedComponents.CreateDefault()
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);

            // First update - child inherits parent's visibility
            system!.Update(0f);

            var childResolvedBefore = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedBefore.IsVisible, Is.False, "Child should inherit parent's visibility (false)");
            Assert.That(childResolvedBefore.SourceEntity, Is.EqualTo(parent), "Source should be parent");

            // Remove parent's visibility component
            world.Remove<PBVisibilityComponent>(parent);
            ref var removedComponents = ref world.Get<RemovedComponents>(parent);
            removedComponents.Remove<PBVisibilityComponent>();

            // Act
            system.Update(0f);

            // Assert
            var childResolvedAfter = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedAfter.IsVisible, Is.True, "Child should reset to visible after parent loses visibility");
        }

        #endregion

        #region Basic Propagation Tests

        [Test]
        public void PropagatesVisibilityToChildren()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);

            // Act
            system!.Update(0f);

            // Assert
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.True, "Child should have ResolvedVisibilityComponent");
            var childResolved = world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolved.IsVisible, Is.False, "Child should inherit parent's visibility");
            Assert.That(childResolved.SourceEntity, Is.EqualTo(parent), "Source should be parent");
            Assert.That(childResolved.ShouldPropagate, Is.True, "ShouldPropagate should be true");
        }

        [Test]
        public void DoesNotPropagateWhenFlagIsFalse()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = false, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);

            // Act
            system!.Update(0f);

            // Assert
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.False, "Child should not have ResolvedVisibilityComponent when propagation is disabled");
        }

        [Test]
        public void PropagatesVisibilityToDeepHierarchy()
        {
            // Arrange
            var parentTransform = CreateTransformComponent("Parent");
            var childTransform = CreateTransformComponent("Child");
            var grandchildTransform = CreateTransformComponent("Grandchild");
            var greatGrandchildTransform = CreateTransformComponent("GreatGrandchild");

            Entity parent = world.Create(
                new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true },
                parentTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity child = world.Create(
                childTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity grandchild = world.Create(
                grandchildTransform,
                new SDKTransform { IsDirty = false }
            );

            Entity greatGrandchild = world.Create(
                greatGrandchildTransform,
                new SDKTransform { IsDirty = false }
            );

            SetupParentChild(parent, child, parentTransform, childTransform);
            SetupParentChild(child, grandchild, childTransform, grandchildTransform);
            SetupParentChild(grandchild, greatGrandchild, grandchildTransform, greatGrandchildTransform);

            // Act
            system!.Update(0f);

            // Assert
            var childResolved = world.Get<ResolvedVisibilityComponent>(child);
            var grandchildResolved = world.Get<ResolvedVisibilityComponent>(grandchild);
            var greatGrandchildResolved = world.Get<ResolvedVisibilityComponent>(greatGrandchild);

            Assert.That(childResolved.IsVisible, Is.False);
            Assert.That(grandchildResolved.IsVisible, Is.False);
            Assert.That(greatGrandchildResolved.IsVisible, Is.False);

            Assert.That(childResolved.SourceEntity, Is.EqualTo(parent));
            Assert.That(grandchildResolved.SourceEntity, Is.EqualTo(parent));
            Assert.That(greatGrandchildResolved.SourceEntity, Is.EqualTo(parent));
        }

        #endregion
    }
}

