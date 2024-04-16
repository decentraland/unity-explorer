using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

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

            rootEntity = world.Create(sceneRoot);
            parentEntity = world.Create(parentSDKTransform, parentTransformComponent);
            childEntity = world.Create(childSDKTransform, childTransformComponent);

            crdtToEntityDict = new Dictionary<CRDTEntity, Entity>
            {
                { sceneRootCRDT, rootEntity },
                { parentCRDTEntity, parentEntity },
                { childCRDTEntity, childEntity },
            };

            system = new ParentingTransformSystem(world, crdtToEntityDict, world.Reference(rootEntity), new SceneShortInfo(Vector2Int.zero, "TEST"));
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
    }
}
