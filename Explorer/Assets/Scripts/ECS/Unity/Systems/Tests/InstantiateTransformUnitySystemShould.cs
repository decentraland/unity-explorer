using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.ComponentsPooling;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Systems.Tests
{
    [TestFixture]
    public class InstantiateTransformUnitySystemShould
    {

        private InstantiateTransformUnitySystem system;
        private SDKTransform sdkTransform;
        private World world;
        private IComponentPoolsRegistry componentRegistry;
        private IComponentPool gameObjectPool;

        [SetUp]
        public void SetUp()
        {
            componentRegistry = new ComponentPoolsRegistry(new Dictionary<Type, IComponentPool>
                { { typeof(GameObject), new UnityGameObjectPool() } }
            );

            world = World.Create();
            system = new InstantiateTransformUnitySystem(world, componentRegistry);
        }


        [Test]
        public void InstantiateTransformComponent()
        {
            // Arrange
            world.Create(new SDKTransform());
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
            componentRegistry.Dispose();
            system.Dispose();
            world.Dispose();
        }
    }
}
