using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Transforms.Tests
{
    [TestFixture]
    public class InstantiateTransformUnitySystemShould : UnitySystemTestBase<InstantiateTransformSystem>
    {
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

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testTransform.gameObject);
        }

        private SDKTransform sdkTransform;
        private IComponentPoolsRegistry componentRegistry;
        private IComponentPool<Transform> transformPool;
        private Transform testTransform;

        [Test]
        public void InstantiateTransformComponent()
        {
            // Arrange
            world.Create(sdkTransform, new CRDTEntity(10));
            QueryDescription entityWithoutUnityTransform = new QueryDescription().WithAll<SDKTransform>().WithNone<TransformComponent>();
            Assert.AreEqual(1, world.CountEntities(in entityWithoutUnityTransform));

            // Act
            system.Update(0f);

            // Assert
            QueryDescription entityWithUnityTransform = new QueryDescription().WithAll<SDKTransform, TransformComponent>();
            Assert.AreEqual(1, world.CountEntities(in entityWithUnityTransform));
            Assert.AreEqual(0, world.CountEntities(in entityWithoutUnityTransform));
        }
    }
}
