using CrdtEcsBridge.Components.Transform;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Transforms.Tests
{

    public class UpdateTransformSystemShould : UnitySystemTestBase<UpdateTransformSystem>
    {

        public void SetUp()
        {
            sdkTransform = new SDKTransform
            {
                IsDirty = true,
                Position = TEST_VECTOR,
            };

            testTransformComponent = new TransformComponent(new GameObject().transform);

            world.Create(sdkTransform, testTransformComponent);

            system = new UpdateTransformSystem(world);
        }


        public void TearDown()
        {
            Object.DestroyImmediate(testTransformComponent.Transform.gameObject);
        }

        private SDKTransform sdkTransform;
        private TransformComponent testTransformComponent;

        private readonly Vector3 TEST_VECTOR = new (1, 2, 3);


        public void UpdateDirtyTransformComponent()
        {
            // Act
            system.Update(0f);

            // Assert
            Assert.IsFalse(sdkTransform.IsDirty);
            Assert.AreEqual(testTransformComponent.Transform.position, TEST_VECTOR);
        }
    }
}
