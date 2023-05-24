using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.ComponentsPooling;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Systems.Tests
{
    [TestFixture]
    public class InstantiateTransformUnitySystemShould : UnitySystemTestBase<InstantiateTransformSystem>
    {
        private SDKTransform sdkTransform;
        private IComponentPoolsRegistry componentRegistry;
        private IComponentPool<Transform> transformPool;
        private Transform testTransform;

        [SetUp]
        public void SetUp()
        {
            transformPool = Substitute.For<IComponentPool<Transform>>();
            transformPool.Get().Returns(testTransform = new GameObject().transform);

            componentRegistry = Substitute.For<IComponentPoolsRegistry>();
            componentRegistry.GetReferenceTypePool<Transform>().Returns(transformPool);

            sdkTransform = new SDKTransform();
            system = new InstantiateTransformSystem(world, componentRegistry);
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
            QueryDescription entityWithUnityTransform = new QueryDescription().WithAll<SDKTransform, TransformComponent>();
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
