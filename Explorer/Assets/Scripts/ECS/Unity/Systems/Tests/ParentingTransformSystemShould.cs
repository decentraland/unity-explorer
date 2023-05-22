using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Systems.Tests
{
    [TestFixture]
    public class ParentingTransformSystemShould
    {
        private SDKTransform parentSDKTransform;
        private SDKTransform childSDKTransform;

        private Transform parentTransform;
        private Transform childTransform;
        private Transform sceneRoot;

        private CRDTEntity sceneRootCRDT;
        private CRDTEntity parentCRDTEntity;
        private CRDTEntity childCRDTEntity;

        private Entity rootEntity;
        private Entity parentEntity;
        private Entity childEntity;

        private World world;
        private ParentingTransformSystem system;
        private Dictionary<CRDTEntity, Entity> crdtToEntityDict;

        [SetUp]
        public void SetUp()
        {
            sceneRootCRDT = new CRDTEntity(0);
            parentCRDTEntity = new CRDTEntity(512);
            childCRDTEntity = new CRDTEntity(513);

            sceneRoot = new GameObject().transform;
            parentTransform = new GameObject().transform;
            childTransform = new GameObject().transform;

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

            world = World.Create();
            rootEntity = world.Create(sceneRoot);
            parentEntity = world.Create(parentSDKTransform, parentTransform);
            childEntity = world.Create(childSDKTransform, childTransform);

            crdtToEntityDict = new Dictionary<CRDTEntity, Entity>
            {
                { sceneRootCRDT, rootEntity },
                { parentCRDTEntity, parentEntity },
                { childCRDTEntity, childEntity },
            };

            system = new ParentingTransformSystem(world, crdtToEntityDict, sceneRoot);
        }

        [Test]
        public void ParentTransform()
        {
            // Act
            system.Update(0f);

            // Assert
            Assert.AreEqual(1, sceneRoot.childCount);
            Assert.AreEqual(1, parentTransform.childCount);
            Assert.AreEqual(parentTransform.GetChild(0), childTransform);
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
            Assert.AreEqual(2, sceneRoot.childCount);
            Assert.AreEqual(0, parentTransform.childCount);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }
    }
}
