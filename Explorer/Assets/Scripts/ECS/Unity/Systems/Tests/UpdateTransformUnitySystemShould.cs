using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Systems.Tests
{
    [TestFixture]
    public class UpdateTransformUnitySystemShould
    {
        private UpdateTransformUnitySystem system;
        private SDKTransform sdkTransform;
        private Transform testTransform;
        private World world;

        private readonly Vector3 TEST_VECTOR = new (1, 2, 3);

        [SetUp]
        public void SetUp()
        {
            sdkTransform = new SDKTransform
            {
                IsDirty = true,
                Position = TEST_VECTOR,
            };

            testTransform = new GameObject().transform;

            world = World.Create();
            world.Create(sdkTransform, testTransform);

            system = new UpdateTransformUnitySystem(world);
        }

        [Test]
        public void UpdateDirtyTransformComponent()
        {
            // Act
            system.Update(0f);

            // Assert
            Assert.IsFalse(sdkTransform.IsDirty);
            Assert.AreEqual(testTransform.position, TEST_VECTOR);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testTransform.gameObject);
            world.Dispose();
        }
    }
}
