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
        public void OrphanAllChildrenWhenDeletedEntityHasMultipleChildren()
        {
            // Arrange: parent with 3 children (A, B, C) — parent gets deleted
            var parentUiTransformComponent = new UITransformComponent();
            parentUiTransformComponent.InitializeAsRoot(new VisualElement());
            var parentSdkEntity = new CRDTEntity(100);
            Entity parentEntity = world.Create(parentSdkEntity, parentUiTransformComponent);
            entitiesMap.Add(parentSdkEntity, parentEntity);

            var childA = new UITransformComponent();
            var childASdk = new CRDTEntity(200);
            childA.InitializeAsChild("A", childASdk, -1);
            Entity childAEntity = world.Create(childASdk, childA, new PBUiTransform());
            entitiesMap.Add(childASdk, childAEntity);

            var childB = new UITransformComponent();
            var childBSdk = new CRDTEntity(300);
            childB.InitializeAsChild("B", childBSdk, 200);
            Entity childBEntity = world.Create(childBSdk, childB, new PBUiTransform());
            entitiesMap.Add(childBSdk, childBEntity);

            var childC = new UITransformComponent();
            var childCSdk = new CRDTEntity(400);
            childC.InitializeAsChild("C", childCSdk, 300);
            Entity childCEntity = world.Create(childCSdk, childC, new PBUiTransform());
            entitiesMap.Add(childCSdk, childCEntity);

            parentUiTransformComponent.RelationData.AddChild(parentEntity, childASdk, ref childA.RelationData);
            parentUiTransformComponent.RelationData.AddChild(parentEntity, childBSdk, ref childB.RelationData);
            parentUiTransformComponent.RelationData.AddChild(parentEntity, childCSdk, ref childC.RelationData);

            Assert.AreEqual(3, parentUiTransformComponent.RelationData.NodeCount);

            // Mark parent for deletion
            world.Add(parentEntity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert: all children should be removed from the deleted parent
            Assert.That(parentUiTransformComponent.RelationData.ContainsNode(childASdk), Is.False);
            Assert.That(parentUiTransformComponent.RelationData.ContainsNode(childBSdk), Is.False);
            Assert.That(parentUiTransformComponent.RelationData.ContainsNode(childCSdk), Is.False);
        }
    }
}
