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
        private Dictionary<CRDTEntity, Entity> entitiesMap = null!;
        private Entity sceneRoot;

        [SetUp]
        public void SetUp()
        {
            sceneRoot = world.Create(new SceneRootComponent());
            system = new UITransformParentingSystem(world, entitiesMap = new Dictionary<CRDTEntity, Entity>(), sceneRoot);
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
        public void ReparentEveryChildOfADeletedEntityToTheSceneRoot()
        {
            // Arrange - the scene root can take children, and a parent with two children is being deleted
            var sceneRootComponent = new UITransformComponent();
            sceneRootComponent.InitializeAsRoot(new VisualElement());
            world.Add(sceneRoot, sceneRootComponent);

            var parentComponent = new UITransformComponent();
            parentComponent.InitializeAsRoot(new VisualElement());
            var parentSdkEntity = new CRDTEntity(100);
            Entity parentEntity = world.Create(parentSdkEntity, parentComponent, new PBUiTransform(), new DeleteEntityIntention());
            entitiesMap[parentSdkEntity] = parentEntity;

            UITransformComponent childA = CreateAttachedChild(200, 0, parentEntity, parentComponent);
            UITransformComponent childB = CreateAttachedChild(300, 200, parentEntity, parentComponent);

            // Act
            system.Update(0);

            // Assert
            Assert.That(childA.RelationData.parent, Is.EqualTo(sceneRoot));
            Assert.That(childB.RelationData.parent, Is.EqualTo(sceneRoot));
            Assert.That(sceneRootComponent.ContentContainer.Contains(childA.Transform), Is.True);
            Assert.That(sceneRootComponent.ContentContainer.Contains(childB.Transform), Is.True);
            Assert.That(parentComponent.RelationData.ContainsNode(new CRDTEntity(200)), Is.False);
            Assert.That(parentComponent.RelationData.ContainsNode(new CRDTEntity(300)), Is.False);
        }

        private UITransformComponent CreateAttachedChild(int crdtId, int rightOf, Entity parentEntity, UITransformComponent parentComponent)
        {
            var childSdkEntity = new CRDTEntity(crdtId);
            var childComponent = new UITransformComponent();
            childComponent.InitializeAsChild("Child", childSdkEntity, new CRDTEntity(rightOf));
            Entity childEntity = world.Create(childSdkEntity, childComponent, new PBUiTransform());
            entitiesMap[childSdkEntity] = childEntity;

            parentComponent.RelationData.AddChild(parentEntity, childSdkEntity, ref childComponent.RelationData);
            parentComponent.ContentContainer.Add(childComponent.Transform);
            return childComponent;
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
            // Arrange - the parent entity exists in the world but its entitiesMap registration lands a tick later than the child's model.
            var parentSdkEntity = new CRDTEntity(300);
            var parentUiTransformComponent = new UITransformComponent();
            parentUiTransformComponent.InitializeAsRoot(new VisualElement());
            Entity parentEntity = world.Create(parentSdkEntity, parentUiTransformComponent);

            var childUiTransformComponent = new UITransformComponent();
            childUiTransformComponent.InitializeAsChild("Child", new CRDTEntity(400), new CRDTEntity(0));
            var childSdkEntity = new CRDTEntity(400);
            var childModel = new PBUiTransform { IsDirty = true, Parent = parentSdkEntity.Id };
            Entity childEntity = world.Create(childSdkEntity, childModel, childUiTransformComponent);
            entitiesMap[childSdkEntity] = childEntity;

            // Act
            system.Update(0);

            Assert.That(childUiTransformComponent.RelationData.parent, Is.EqualTo(Entity.Null),
                "child must still be unattached while its parent is not resolvable");

            // The dirty flag is reset at the end of every frame whether or not parenting succeeded.
            childModel.IsDirty = false;

            // The parent resolves later without the child's model being re-dirtied.
            entitiesMap[parentSdkEntity] = parentEntity;

            // Act
            system.Update(0);

            // Assert
            Assert.That(childUiTransformComponent.RelationData.parent, Is.EqualTo(parentEntity));
            Assert.That(parentUiTransformComponent.ContentContainer.Contains(childUiTransformComponent.Transform), Is.True);
        }

        [Test]
        public void RestoreVisibilityOnceChildIsAttached()
        {
            // Arrange - the Hidden state left by UITransformInstantiationSystem, reproduced by hand to keep this fixture free of a UIDocument.
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
                "element must start Hidden for the restore to be observable");

            // Act
            system.Update(0);

            // Assert
            Assert.That(parentComponent.ContentContainer.Contains(childUiTransformComponent.Transform), Is.True);

            // A cleared inline visibility (StyleKeyword.Null) hands control back to the stylesheet.
            Assert.That(childUiTransformComponent.Transform.style.visibility.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
