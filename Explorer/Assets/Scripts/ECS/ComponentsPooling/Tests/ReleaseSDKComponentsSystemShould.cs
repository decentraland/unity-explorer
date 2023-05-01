using Arch.Core;
using ECS.LifeCycle.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using Random = UnityEngine.Random;

namespace ECS.ComponentsPooling.Tests
{
    [TestFixture]
    public class ReleaseSDKComponentsSystemShould
    {
        public class TestComponent1
        {
            public int v = Random.Range(0, 1000);
        }

        public class TestComponent2
        {
            public float a = Random.Range(100f, 400f);
        }

        [Test]
        public void ReleaseAllComponentsToPools()
        {
            var world = World.Create();

            var componentsPoolRegistry = Substitute.For<IComponentPoolsRegistry>();

            for (var i = 0; i < 100; i++)
                world.Create(new TestComponent1(), new TestComponent2(), new DeleteEntityIntention());

            var system = new ReleaseSDKComponentsSystem(world, componentsPoolRegistry);

            system.Update(0);

            componentsPoolRegistry.Received(100).TryGetPool(Arg.Is<Type>(t => typeof(TestComponent1) == t), out _);
            componentsPoolRegistry.Received(100).TryGetPool(Arg.Is<Type>(t => typeof(TestComponent2) == t), out _);
        }
    }
}
