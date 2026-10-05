using Arch.Core;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.Optimization.Pools.Tests
{
    public class ReleaseReferenceComponentsSystemShould : UnitySystemTestBase<ReleaseReferenceComponentsSystem>
    {
        private IComponentPool pool = null!;

        [SetUp]
        public void SetUp()
        {
            pool = Substitute.For<IComponentPool>();
            var registry = new ComponentPoolsRegistry(new Dictionary<Type, IComponentPool> { { typeof(TestComponent), pool } }, null);
            system = new ReleaseReferenceComponentsSystem(world, registry);
        }

        [Test]
        public void ReleaseOnlyEntitiesMarkedForDeletion()
        {
            // Arrange
            var deleted = new TestComponent();
            var alive = new TestComponent();
            world.Create(deleted, new DeleteEntityIntention());
            world.Create(alive);

            // Act
            system.Update(0);

            // Assert
            pool.Received(1).Release(deleted);
            pool.DidNotReceive().Release(alive);
        }

        [Test]
        public void ReleaseAllComponentsOnWorldFinalization()
        {
            // Arrange
            var deleted = new TestComponent();
            var alive = new TestComponent();
            world.Create(deleted, new DeleteEntityIntention());
            world.Create(alive);

            // Act
            system.FinalizeComponents(world.Query(QueryDescription.Null));

            // Assert
            pool.Received(1).Release(deleted);
            pool.Received(1).Release(alive);
        }

        private class TestComponent { }
    }
}
