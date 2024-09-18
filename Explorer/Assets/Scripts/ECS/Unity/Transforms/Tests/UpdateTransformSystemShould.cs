using Arch.SystemGroups.UnityBridge;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.UpdateGate;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms.Tests
{
    [TestFixture]
    public class UpdateTransformSystemShould : UnitySystemTestBase<UpdateTransformSystem>
    {
        [SetUp]
        public void SetUp()
        {
            sdkTransform = new SDKTransform
            {
                IsDirty = true,
                Position = new CanBeDirty<Vector3>(TEST_VECTOR),
            };

            testTransformComponent = new TransformComponent(new GameObject().transform);

            world.Create(sdkTransform, testTransformComponent);

            var systemGroupsUpdateGate = Substitute.For<ISystemGroupsUpdateGate>();
            systemGroupsUpdateGate.ShouldThrottle(Arg.Any<System.Type>(), Arg.Any<TimeProvider.Info>()).Returns(false);

            var systemsUpdateGate = Substitute.For<ISystemsUpdateGate>();
            systemsUpdateGate.IsOpen<SDKTransform>().Returns(true);

            system = new UpdateTransformSystem(world, systemGroupsUpdateGate, systemsUpdateGate);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testTransformComponent.Transform.gameObject);
        }

        private SDKTransform sdkTransform;
        private TransformComponent testTransformComponent;

        private readonly Vector3 TEST_VECTOR = new (1, 2, 3);

        [Test]
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
