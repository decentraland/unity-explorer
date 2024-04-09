using Arch.Core;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using Random = UnityEngine.Random;

namespace DCL.Optimization.Pools.Tests
{

    public class ReleaseComponentsSystemShould
    {
        public class TestComponent1
        {
            public int v = Random.Range(0, 1000);
        }

        public class TestComponent2
        {
            public float a = Random.Range(100f, 400f);
        }

        public struct ValueType1
        {
            public int i;
        }


        public void ReleaseAllComponentsToPools()
        {
            var world = World.Create();

            IComponentPoolsRegistry componentsPoolRegistry = Substitute.For<IComponentPoolsRegistry>();

            for (var i = 0; i < 100; i++)
                world.Create(new TestComponent1(), new TestComponent2(), new DeleteEntityIntention());

            var system = new ReleaseReferenceComponentsSystem(world, componentsPoolRegistry);

            system.Update(0);

            componentsPoolRegistry.Received(100).TryGetPool(Arg.Is<Type>(t => typeof(TestComponent1) == t), out _);
            componentsPoolRegistry.Received(100).TryGetPool(Arg.Is<Type>(t => typeof(TestComponent2) == t), out _);
        }


        public void IgnoreValueTypes()
        {
            var world = World.Create();

            IComponentPoolsRegistry componentsPoolRegistry = Substitute.For<IComponentPoolsRegistry>();

            for (var i = 0; i < 100; i++)
                world.Create(new ValueType1(), new DeleteEntityIntention());

            var system = new ReleaseReferenceComponentsSystem(world, componentsPoolRegistry);

            system.Update(0);

            componentsPoolRegistry.DidNotReceive().TryGetPool(typeof(ValueType1), out Arg.Any<IComponentPool>());
        }
    }
}
