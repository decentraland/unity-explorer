using Arch.Core;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using Random = UnityEngine.Random;

namespace DCL.Optimization.Pools.Tests
{

    public class ReleasePoolableComponentSystemShould
    {
        public class TestComponent1
        {
            public int v = Random.Range(0, 1000);
        }

        public struct TestProvider : IPoolableComponentProvider<TestComponent1>
        {
            public TestComponent1 Component;

            public TestProvider(TestComponent1 poolableComponent)
            {
                Component = poolableComponent;
            }

            TestComponent1 IPoolableComponentProvider<TestComponent1>.PoolableComponent => Component;

            public Type PoolableComponentType => typeof(TestComponent1);

            public void Dispose() { }
        }


        public void ReleaseAllComponentsToPools()
        {
            var world = World.Create();

            IComponentPoolsRegistry componentsPoolRegistry = Substitute.For<IComponentPoolsRegistry>();
            IComponentPool pool = Substitute.For<IComponentPool>();
            componentsPoolRegistry.GetPool(typeof(TestComponent1)).Returns(pool);

            for (var i = 0; i < 100; i++)
                world.Create(new TestProvider(new TestComponent1()), new DeleteEntityIntention());

            var system =
                new ReleasePoolableComponentSystem<TestComponent1, TestProvider>(world, componentsPoolRegistry);

            system.Update(0);

            componentsPoolRegistry.Received(100).GetPool(typeof(TestComponent1));
            pool.Received(100).Release(Arg.Is<object>(o => o.GetType() == typeof(TestComponent1)));
        }
    }
}
