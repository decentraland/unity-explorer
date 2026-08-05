using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformParentingSystemShould : UnitySystemTestBase<UITransformParentingSystem>
    {
        private Dictionary<CRDTEntity, Entity> entitiesMap;

        [SetUp]
        public void SetUp()
        {
            system = new UITransformParentingSystem(world, entitiesMap = new Dictionary<CRDTEntity, Entity>(), world.Create(new SceneRootComponent()));
        }

        [Test]
        public void RemoveDeletedEntityFromTheParentList()
        {
            var parentUiTransformComponent = new UITransformComponent();
            parentUiTransformComponent.InitializeAsRoot(new VisualElement());
            var parentSdkEntity = new CRDTEntity(100);
            Entity parentEntity = world.Create(parentSdkEntity, parentUiTransformComponent);

            entitiesMap.Add(parentSdkEntity, parentEntity);

            var childUiTransformComponent = new UITransformComponent();
            var childSdkEntity = new CRDTEntity(200);
            childUiTransformComponent.InitializeAsChild("TEST", childSdkEntity, -1);
            childUiTransformComponent.RelationData.parent = parentEntity;
            Entity childEntity = world.Create(childSdkEntity, childUiTransformComponent, new DeleteEntityIntention(), new PBUiTransform());

            parentUiTransformComponent.RelationData.AddChild(parentEntity, childSdkEntity, ref childUiTransformComponent.RelationData);

            entitiesMap.Add(childSdkEntity, childEntity);

            system.Update(0);

            Assert.That(parentUiTransformComponent.RelationData.ContainsNode(childSdkEntity), Is.False);
            Assert.That(parentUiTransformComponent.RelationData.head, Is.Null);
        }

        [Test]
        public void AddChildToContentContainer_WhenParentHasOverflowScroll()
        {
            // Arrange — parent with overflow scroll (inner ScrollView already set)
            var parentTransform = new VisualElement();
            var scrollView = new ScrollView();
            parentTransform.Add(scrollView);
            var parentComponent = new UITransformComponent();
            parentComponent.InitializeAsRoot(parentTransform);
            parentComponent.InnerScrollView = scrollView;

            var parentSdkEntity = new CRDTEntity(1);
            Entity parentEntity = world.Create(parentSdkEntity, parentComponent);
            entitiesMap[parentSdkEntity] = parentEntity;

            var childComponent = new UITransformComponent();
            childComponent.InitializeAsChild("Child", new CRDTEntity(2), new CRDTEntity(0));
            var childSdkEntity = new CRDTEntity(2);
            Entity childEntity = world.Create(childSdkEntity, new PBUiTransform { IsDirty = true, Parent = parentSdkEntity.Id }, childComponent);
            entitiesMap[childSdkEntity] = childEntity;

            // Act
            system.Update(0);

            // Assert — child was added to ContentContainer, i.e. ScrollView's contentContainer
            Assert.That(parentComponent.ContentContainer, Is.SameAs(scrollView.contentContainer));
            Assert.That(parentComponent.ContentContainer.Contains(childComponent.Transform), Is.True);
            Assert.That(scrollView.contentContainer.Contains(childComponent.Transform), Is.True);
        }

        [Test]
        public void AttachChildOnceUnresolvedParentAppearsInEntitiesMap()
        {
            // Arrange — the parent's Arch entity + UITransformComponent already exist in the
            // world (as they would once WorldSyncCommandBuffer creates it for the parent's CRDT
            // batch), but its entitiesMap registration is DELAYED — reproducing the tick window
            // where a child's PBUiTransform PUT lands before its parent's first component PUT
            // is reflected in entitiesMap (report.md step 4).
            var parentSdkEntity = new CRDTEntity(300);
            var parentUiTransformComponent = new UITransformComponent();
            parentUiTransformComponent.InitializeAsRoot(new VisualElement());
            Entity parentEntity = world.Create(parentSdkEntity, parentUiTransformComponent);
            // entitiesMap.Add(parentSdkEntity, parentEntity) intentionally NOT called yet.

            var childUiTransformComponent = new UITransformComponent();
            childUiTransformComponent.InitializeAsChild("Child", new CRDTEntity(400), new CRDTEntity(0));
            var childSdkEntity = new CRDTEntity(400);
            var childModel = new PBUiTransform { IsDirty = true, Parent = parentSdkEntity.Id };
            Entity childEntity = world.Create(childSdkEntity, childModel, childUiTransformComponent);
            entitiesMap[childSdkEntity] = childEntity;

            // Act 1 — pump while the parent is still unresolved in entitiesMap: the dirty pass
            // (and, with the fix, the retry pass) both miss entitiesMap.TryGetValue, so the
            // child stays unattached.
            system.Update(0);

            Assert.That(childUiTransformComponent.RelationData.parent, Is.EqualTo(Entity.Null),
                "parent is not resolvable yet — child must still be unattached after the first pump");

            // Simulate ResetDirtyFlagSystem<PBUiTransform>, which unconditionally clears IsDirty
            // at the end of every frame regardless of whether any consumer succeeded
            // (SceneUIPlugin.cs:104, ResetDirtyFlagSystem.cs:28-32).
            childModel.IsDirty = false;

            // The parent's entitiesMap entry now appears (its CRDT batch lands a tick later) —
            // WITHOUT re-dirtying the child's PBUiTransform, exactly as happens in the real race.
            entitiesMap[parentSdkEntity] = parentEntity;

            // Act 2
            system.Update(0);

            // Assert — the child must now be attached under the parent's ContentContainer.
            // At the pin this FAILS: DoUITransformParenting's early return on !sdkModel.IsDirty
            // skips the child forever (no retry path exists), so RelationData.parent stays
            // Entity.Null and the transform is never added to the parent's ContentContainer —
            // reproducing the permanently orphaned quest-timer digits.
            Assert.That(childUiTransformComponent.RelationData.parent, Is.EqualTo(parentEntity));
            Assert.That(parentUiTransformComponent.ContentContainer.Contains(childUiTransformComponent.Transform), Is.True);
        }

        [Test]
        public void RestoreVisibilityOnceChildIsAttached()
        {
            // Arrange — a child whose Transform was left Hidden by instantiation (this is what
            // the patched UITransformInstantiationSystem does before the element is parented;
            // reproduced by hand here so this test stays inside the lightweight harness used by
            // the rest of this fixture, with no UIDocument/canvas needed).
            var childUiTransformComponent = new UITransformComponent();
            childUiTransformComponent.InitializeAsChild("Child", new CRDTEntity(2), new CRDTEntity(0));
            childUiTransformComponent.Transform.style.visibility = Visibility.Hidden;
            var childSdkEntity = new CRDTEntity(2);

            var parentComponent = new UITransformComponent();
            parentComponent.InitializeAsRoot(new VisualElement());
            var parentSdkEntity = new CRDTEntity(1);
            Entity parentEntity = world.Create(parentSdkEntity, parentComponent);
            entitiesMap[parentSdkEntity] = parentEntity;

            Entity childEntity = world.Create(childSdkEntity, new PBUiTransform { IsDirty = true, Parent = parentSdkEntity.Id }, childUiTransformComponent);
            entitiesMap[childSdkEntity] = childEntity;

            Assert.That(childUiTransformComponent.Transform.style.visibility.value, Is.EqualTo(Visibility.Hidden),
                "sanity: element starts Hidden, as InstantiateUITransform leaves it before it is parented");

            // Act — a normal, resolvable-parent attach.
            system.Update(0);

            // Assert — attach succeeded (pre-existing behavior) ...
            Assert.That(parentComponent.ContentContainer.Contains(childUiTransformComponent.Transform), Is.True);

            // ... and, only with the fix, visibility was restored. At the pin nothing under
            // SceneUI ever touches style.visibility, so the element remains Hidden forever even
            // though it is now correctly parented.
            Assert.That(childUiTransformComponent.Transform.style.visibility.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
