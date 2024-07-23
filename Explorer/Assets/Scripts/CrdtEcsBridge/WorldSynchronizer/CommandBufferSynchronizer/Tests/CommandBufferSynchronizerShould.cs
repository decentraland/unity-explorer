using Arch.Buffer;
using Arch.Core;
using CRDT.Protocol;
using CrdtEcsBridge.WorldSynchronizer.CommandBuffer;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using NSubstitute;
using NUnit.Framework;

namespace CrdtEcsBridge.WorldSynchronizer.CommandBufferSynchronizer.Tests
{
    [TestFixture]
    public class CommandBufferSynchronizerShould
    {
        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            commandBuffer = new PersistentCommandBuffer();
            componentPool = Substitute.For<IComponentPool<TestComponent>>();

            commandBufferSynchronizer = new SDKComponentCommandBufferSynchronizer<TestComponent>(componentPool);
        }

        [TearDown]
        public void TearDown()
        {
            commandBuffer.Dispose();
            world.Dispose();
        }

        public class TestComponent
        {
            public int Value;
        }

        private World world;
        private PersistentCommandBuffer commandBuffer;
        private IComponentPool<TestComponent> componentPool;
        private Entity entity;

        private SDKComponentCommandBufferSynchronizer<TestComponent> commandBufferSynchronizer;

        [Test]
        public void ApplyModifiedComponent()
        {
            entity = world.Create(new TestComponent { Value = 100 });

            commandBufferSynchronizer.Apply(world, commandBuffer, entity, CRDTReconciliationEffect.ComponentModified, new TestComponent { Value = 200 }, false);
            commandBuffer.Playback(world);

            componentPool.Received(1).Release(Arg.Is<TestComponent>(t => t.Value == 100));
            Assert.AreEqual(200, world.Get<TestComponent>(entity).Value);
        }

        [Test]
        public void ApplyAddedComponent()
        {
            entity = world.Create();

            commandBufferSynchronizer.Apply(world, commandBuffer, entity, CRDTReconciliationEffect.ComponentAdded, new TestComponent { Value = 300 }, false);
            commandBuffer.Playback(world);

            componentPool.DidNotReceive().Release(Arg.Any<TestComponent>());
            Assert.AreEqual(300, world.Get<TestComponent>(entity).Value);
        }

        [Test]
        public void ApplyDeletedComponent()
        {
            entity = world.Create(new TestComponent { Value = 100 }, RemovedComponents.CreateDefault());

            commandBufferSynchronizer.Apply(world, commandBuffer, entity, CRDTReconciliationEffect.ComponentDeleted, null, false);
            commandBuffer.Playback(world);

            componentPool.Received(1).Release(Arg.Is<TestComponent>(t => t.Value == 100));
            Assert.IsFalse(world.Has<TestComponent>(entity));
        }
    }
}
