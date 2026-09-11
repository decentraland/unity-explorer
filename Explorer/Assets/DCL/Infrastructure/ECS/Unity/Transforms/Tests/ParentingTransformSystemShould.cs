using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace ECS.Unity.Transforms.Tests
{
    [TestFixture]
    public class ParentingTransformSystemShould : UnitySystemTestBase<ParentingTransformSystem>
    {
        [SetUp]
        public void SetUp()
        {
            sceneRootCRDT = new CRDTEntity(0);
            parentCRDTEntity = new CRDTEntity(512);
            childCRDTEntity = new CRDTEntity(513);

            sceneRoot = new TransformComponent(new GameObject("SCENE_ROOT").transform);
            parentTransformComponent = new TransformComponent(new GameObject("PARENT").transform);
            childTransformComponent = new TransformComponent(new GameObject("CHILD").transform);

            parentSDKTransform = new SDKTransform
            {
                IsDirty = true,
                ParentId = sceneRootCRDT,
            };

            childSDKTransform = new SDKTransform
            {
                IsDirty = true,
                ParentId = parentCRDTEntity,
            };

            rootEntity = world.Create(sceneRoot, sceneRootCRDT);
            parentEntity = world.Create(parentSDKTransform, parentTransformComponent, parentCRDTEntity);
            childEntity = world.Create(childSDKTransform, childTransformComponent, childCRDTEntity);

            crdtToEntityDict = new Dictionary<CRDTEntity, Entity>
            {
                { sceneRootCRDT, rootEntity },
                { parentCRDTEntity, parentEntity },
                { childCRDTEntity, childEntity },
            };

            system = new ParentingTransformSystem(world, crdtToEntityDict, rootEntity);
        }

        private SDKTransform parentSDKTransform;
        private SDKTransform childSDKTransform;

        private TransformComponent parentTransformComponent;
        private TransformComponent childTransformComponent;
        private TransformComponent sceneRoot;

        private CRDTEntity sceneRootCRDT;
        private CRDTEntity parentCRDTEntity;
        private CRDTEntity childCRDTEntity;

        private Entity rootEntity;
        private Entity parentEntity;
        private Entity childEntity;

        private Dictionary<CRDTEntity, Entity> crdtToEntityDict;

        [Test]
        public void ParentTransform()
        {
            // Act
            system.Update(0f);

            // Assert
            Assert.AreEqual(1, sceneRoot.Transform.childCount);
            Assert.AreEqual(1, sceneRoot.Children.Count);
            Assert.AreEqual(1, parentTransformComponent.Transform.childCount);
            Assert.AreEqual(1, parentTransformComponent.Children.Count);
            Assert.AreEqual(parentTransformComponent.Transform.GetChild(0), childTransformComponent.Transform);
        }

        [Test]
        public void UnParentTransform()
        {
            // Arrange
            system.Update(0f);

            childSDKTransform = new SDKTransform
            {
                IsDirty = true,
                ParentId = sceneRootCRDT,
            };

            world.Set(childEntity, childSDKTransform);

            // Act
            system.Update(0f);

            // Assert
            Assert.AreEqual(2, sceneRoot.Transform.childCount);
            Assert.AreEqual(2, sceneRoot.Children.Count);
            Assert.AreEqual(0, parentTransformComponent.Transform.childCount);
            Assert.AreEqual(0, parentTransformComponent.Children.Count);
        }

        [Test]
        public void ParentChildToSceneRootIfParentIsDeleted()
        {
            // Arrange
            //One tick to do the parenting
            system.Update(0f);
            world.Add(parentEntity, new DeleteEntityIntention());
            crdtToEntityDict.Remove(parentCRDTEntity);

            // Act
            system.Update(0f);

            // Assert
            Assert.AreEqual(0, parentTransformComponent.Children.Count);
            Assert.IsTrue(childTransformComponent.Transform.IsChildOf(sceneRoot.Transform));
        }

        [Test]
        public void NotThrowWhenDeletedParentListsDestroyedChild()
        {
            // Arrange
            system.Update(0f);

            // Destroyed without being dereferenced, so the parent still lists it
            world.Destroy(childEntity);
            world.Add(parentEntity, new DeleteEntityIntention());
            crdtToEntityDict.Remove(parentCRDTEntity);

            // Act & Assert
            Assert.DoesNotThrow(() => system.Update(0f));
            Assert.AreEqual(0, parentTransformComponent.Children.Count);
        }

        [Test]
        public void NotTouchStrangerReusingDestroyedChildId()
        {
            // Arrange
            system.Update(0f);
            world.Destroy(childEntity);

            // Arch recycles the id with a bumped version, so the stale entry now resolves to an unrelated entity
            Entity stranger = world.Create(new CRDTEntity(600));
            Assume.That(stranger.Id, Is.EqualTo(childEntity.Id));
            Assume.That(stranger.Version, Is.Not.EqualTo(childEntity.Version));

            world.Add(parentEntity, new DeleteEntityIntention());
            crdtToEntityDict.Remove(parentCRDTEntity);

            // Act & Assert
            Assert.DoesNotThrow(() => system.Update(0f));
            Assert.IsFalse(world.Has<TransformComponent>(stranger));
            Assert.AreEqual(0, parentTransformComponent.Children.Count);
        }

        [Test]
        public void NotReparentStrangerWithTransformReusingDestroyedChildId()
        {
            // Arrange
            system.Update(0f);
            world.Destroy(childEntity);

            var strangerTransform = new TransformComponent(new GameObject("STRANGER").transform);
            Entity stranger = world.Create(new CRDTEntity(600), strangerTransform);
            Assume.That(stranger.Id, Is.EqualTo(childEntity.Id));

            world.Add(parentEntity, new DeleteEntityIntention());
            crdtToEntityDict.Remove(parentCRDTEntity);

            // Act
            Assert.DoesNotThrow(() => system.Update(0f));

            // Assert
            Assert.AreEqual(Entity.Null, world.Get<TransformComponent>(stranger).Parent);
            Assert.IsNull(strangerTransform.Transform.parent);
            Assert.IsFalse(sceneRoot.Children.Contains(stranger));
        }

        [Test]
        public void RemoveFromOldParentWhenReparentedToUnknownParent()
        {
            // Arrange
            system.Update(0f);

            world.Set(childEntity, new SDKTransform
            {
                IsDirty = true,
                ParentId = new CRDTEntity(999),
            });

            // Act
            system.Update(0f);

            // Assert
            Assert.AreEqual(0, parentTransformComponent.Children.Count);
            Assert.IsTrue(sceneRoot.Children.Contains(childEntity));
            Assert.AreEqual(rootEntity, world.Get<TransformComponent>(childEntity).Parent);
            Assert.AreEqual(sceneRoot.Transform, childTransformComponent.Transform.parent);
        }

        [Test]
        public void FallBackToSceneRootWhenParentHasNoTransform()
        {
            // Arrange
            system.Update(0f);

            var noTransformCrdtEntity = new CRDTEntity(700);
            crdtToEntityDict[noTransformCrdtEntity] = world.Create(noTransformCrdtEntity);

            world.Set(childEntity, new SDKTransform
            {
                IsDirty = true,
                ParentId = noTransformCrdtEntity,
            });

            LogAssert.Expect(LogType.Error, new Regex("doesn't have a TransformComponent, falling back to the scene root"));

            // Act
            system.Update(0f);

            // Assert
            Assert.AreEqual(0, parentTransformComponent.Children.Count);
            Assert.IsTrue(sceneRoot.Children.Contains(childEntity));
            Assert.AreEqual(rootEntity, world.Get<TransformComponent>(childEntity).Parent);
        }

        [Test]
        public void DereferenceDeletedChildWithoutSdkTransform()
        {
            // Arrange
            system.Update(0f);

            var noSdkTransform = new TransformComponent(new GameObject("NO_SDK_TRANSFORM").transform);
            Entity noSdkEntity = world.Create(new CRDTEntity(514));
            noSdkTransform.AssignParent(noSdkEntity, parentEntity, in world.Get<TransformComponent>(parentEntity));
            world.Add(noSdkEntity, noSdkTransform, new DeleteEntityIntention());

            // Act
            system.Update(0f);

            // Assert
            Assert.IsFalse(parentTransformComponent.Children.Contains(noSdkEntity));
        }
    }
}
