using Arch.Core;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace ECS.LifeCycle.Tests
{

    public class ReleaseRemovedComponentsSystemShould : UnitySystemTestBase<ReleaseRemovedComponentsSystem>
    {

        public void SetUp()
        {
            system = new ReleaseRemovedComponentsSystem(world);
        }

        public class TestComponent1
        {
            public int v = Random.Range(0, 1000);
        }


        public void Dispose()
        {
            Entity e = world.Create(new TestComponent1(), RemovedComponents.CreateDefault());

            world.Get<RemovedComponents>(e).Set.Add(typeof(TestComponent1));
            Assert.That(world.Get<RemovedComponents>(e).Set.Count, Is.EqualTo(1));

            system.FinalizeComponents(new Query());

            Assert.That(world.Get<RemovedComponents>(e).Set.Count, Is.EqualTo(0));
        }
    }
}
