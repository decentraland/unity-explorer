using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.ComponentsPooling;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Systems.Tests
{
    [TestFixture]
    public class InstantiateTransformUnitySystemShould : UnitySystemTestBase<InstantiateTransformUnitySystem>
    {
        private SDKTransform sdkTransform;
        private IComponentPoolsRegistry componentRegistry;
        private IComponentPool transformPool;
        private Transform testTransform;

        [SetUp]
        public void SetUp()
        {
            transformPool = Substitute.For<IComponentPool<Transform>>();
            transformPool.Rent().Returns(testTransform = new GameObject().transform);

            componentRegistry = Substitute.For<IComponentPoolsRegistry>();
            componentRegistry.GetReferenceTypePool<Transform>().Returns(transformPool);

            sdkTransform = new SDKTransform();
            system = new InstantiateTransformUnitySystem(world, componentRegistry);
        }

        [Test]
        public void InstantiateTransformComponent()
        {
            // Arrange
            world.Create(sdkTransform);
            QueryDescription entityWithoutUnityTransform = new QueryDescription().WithExclusive<SDKTransform>();
            Assert.AreEqual(1, world.CountEntities(in entityWithoutUnityTransform));

            // Act
            system.Update(0f);

            // Assert
            QueryDescription entityWithUnityTransform = new QueryDescription().WithAll<SDKTransform, Transform>();
            Assert.AreEqual(1, world.CountEntities(in entityWithUnityTransform));
            Assert.AreEqual(0, world.CountEntities(in entityWithoutUnityTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testTransform.gameObject);
        }
    }
}
