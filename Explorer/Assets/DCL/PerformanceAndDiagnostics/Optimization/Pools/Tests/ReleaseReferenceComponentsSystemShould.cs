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
        public void ReleaseOnlyEntitiesWhoseDeletionIsNotDeferred()
        {
            var deferred = new TestComponent();
            var ready = new TestComponent();
            var alive = new TestComponent();
            Entity deferredEntity = world.Create(deferred, new DeleteEntityIntention { DeferDeletion = true });
            Entity readyEntity = world.Create(ready, new DeleteEntityIntention());
            world.Create(alive);

            system.Update(0);

            pool.Received(1).Release(ready);
            pool.DidNotReceive().Release(deferred);
            pool.DidNotReceive().Release(alive);

            world.Destroy(readyEntity);
            world.Set(deferredEntity, new DeleteEntityIntention());
            system.Update(0);

            pool.Received(1).Release(deferred);
            pool.DidNotReceive().Release(alive);
        }

        [Test]
        public void ReleaseAllComponentsOnWorldFinalizationIncludingDeferredEntities()
        {
            var deferred = new TestComponent();
            var alive = new TestComponent();
            world.Create(deferred, new DeleteEntityIntention { DeferDeletion = true });
            world.Create(alive);

            system.FinalizeComponents(world.Query(QueryDescription.Null));

            pool.Received(1).Release(deferred);
            pool.Received(1).Release(alive);
        }

        private class TestComponent { }
    }
}
