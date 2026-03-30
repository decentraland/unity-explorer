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
    }
}
