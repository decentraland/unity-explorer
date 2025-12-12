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
            // Clean up created GameObjects [[memory:3702833]]
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);

            // Act
            system!.Update(0f);

            // Assert
            ref var childResolved = ref world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolved.IsVisible, Is.True, "Child's own visibility should take priority");
            Assert.That(childResolved.SourceEntity, Is.EqualTo(child), "Source should be child itself");
        }

        [Test]
        public void PropagationSkipsChildWithOwnVisibilityComponent()
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);
            world.Set(grandchild, grandchildTransform);

            // Act
            system!.Update(0f);

            // Assert - grandchild should NOT have inherited visibility because child has own component (without propagate)
            bool hasGrandchildResolved = world.Has<ResolvedVisibilityComponent>(grandchild);
            Assert.That(hasGrandchildResolved, Is.False, "Grandchild should not inherit when intermediate child has own visibility without propagation");
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);

            // First update - both have their own visibility
            system!.Update(0f);

            ref var childResolvedBefore = ref world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedBefore.IsVisible, Is.True, "Child should be visible before removal");

            // Remove child's visibility component
            world.Remove<PBVisibilityComponent>(child);
            ref var removedComponents = ref world.Get<RemovedComponents>(child);
            removedComponents.Set.Add(typeof(PBVisibilityComponent));

            // Act
            system.Update(0f);

            // Assert
            ref var childResolvedAfter = ref world.Get<ResolvedVisibilityComponent>(child);
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);
            world.Set(grandchild, grandchildTransform);

            // Act
            system!.Update(0f);

            // Assert
            ref var grandchildResolved = ref world.Get<ResolvedVisibilityComponent>(grandchild);
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
            world.Set(child, childTransform);
            world.Set(oldParent, oldParentTransform);

            // First update - child inherits old parent's visibility
            system!.Update(0f);

            ref var childResolvedBefore = ref world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedBefore.IsVisible, Is.False, "Child should inherit old parent's visibility");

            // Reparent child to new parent
            oldParentTransform.Children.Remove(child);
            childTransform.Transform.SetParent(newParentTransform.Transform, true);
            childTransform.Parent = newParent;
            newParentTransform.Children.Add(child);

            world.Set(child, childTransform);
            world.Set(oldParent, oldParentTransform);
            world.Set(newParent, newParentTransform);

            // Mark transform as dirty to trigger reparenting detection
            ref var childSdkTransform = ref world.Get<SDKTransform>(child);
            childSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert
            ref var childResolvedAfter = ref world.Get<ResolvedVisibilityComponent>(child);
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
            world.Set(child, childTransform);
            world.Set(oldParent, oldParentTransform);

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

            world.Set(child, childTransform);
            world.Set(oldParent, oldParentTransform);
            world.Set(newParent, newParentTransform);

            // Mark transform as dirty to trigger reparenting detection
            ref var childSdkTransform = ref world.Get<SDKTransform>(child);
            childSdkTransform.IsDirty = true;

            // Act
            system.Update(0f);

            // Assert
            ref var childResolvedAfter = ref world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedAfter.IsVisible, Is.False, "Child should inherit new parent's visibility (false)");
            Assert.That(childResolvedAfter.SourceEntity, Is.EqualTo(newParent), "Source should be new parent");
            Assert.That(childResolvedAfter.ShouldPropagate, Is.True, "ShouldPropagate should be true");
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);

            // First update - child inherits parent's visibility
            system!.Update(0f);

            ref var childResolvedBefore = ref world.Get<ResolvedVisibilityComponent>(child);
            Assert.That(childResolvedBefore.IsVisible, Is.False, "Child should inherit parent's visibility (false)");
            Assert.That(childResolvedBefore.SourceEntity, Is.EqualTo(parent), "Source should be parent");

            // Remove parent's visibility component
            world.Remove<PBVisibilityComponent>(parent);
            ref var removedComponents = ref world.Get<RemovedComponents>(parent);
            removedComponents.Set.Add(typeof(PBVisibilityComponent));

            // Act
            system.Update(0f);

            // Assert
            ref var childResolvedAfter = ref world.Get<ResolvedVisibilityComponent>(child);
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);

            // Act
            system!.Update(0f);

            // Assert
            Assert.That(world.Has<ResolvedVisibilityComponent>(child), Is.True, "Child should have ResolvedVisibilityComponent");
            ref var childResolved = ref world.Get<ResolvedVisibilityComponent>(child);
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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);

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
            world.Set(child, childTransform);
            world.Set(parent, parentTransform);
            world.Set(grandchild, grandchildTransform);
            world.Set(greatGrandchild, greatGrandchildTransform);

            // Act
            system!.Update(0f);

            // Assert
            ref var childResolved = ref world.Get<ResolvedVisibilityComponent>(child);
            ref var grandchildResolved = ref world.Get<ResolvedVisibilityComponent>(grandchild);
            ref var greatGrandchildResolved = ref world.Get<ResolvedVisibilityComponent>(greatGrandchild);

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

