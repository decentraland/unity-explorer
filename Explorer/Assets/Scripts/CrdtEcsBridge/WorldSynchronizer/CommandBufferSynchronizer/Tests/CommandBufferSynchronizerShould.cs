using Arch.CommandBuffer;
using Arch.Core;
using CRDT.Protocol;
using CrdtEcsBridge.WorldSynchronizer.CommandBuffer;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using NSubstitute;
using NUnit.Framework;

namespace CrdtEcsBridge.WorldSynchronizer.CommandBufferSynchronizer.Tests
{

    public class CommandBufferSynchronizerShould
    {

        public void SetUp()
        {
            world = World.Create();
            commandBuffer = new PersistentCommandBuffer(world);
            componentPool = Substitute.For<IComponentPool<TestComponent>>();

            commandBufferSynchronizer = new SDKComponentCommandBufferSynchronizer<TestComponent>(componentPool);
        }


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


        public void ApplyModifiedComponent()
        {
            entity = world.Create(new TestComponent { Value = 100 });

            commandBufferSynchronizer.Apply(world, commandBuffer, entity, CRDTReconciliationEffect.ComponentModified, new TestComponent { Value = 200 });
            commandBuffer.Playback();

            componentPool.Received(1).Release(Arg.Is<TestComponent>(t => t.Value == 100));
            Assert.AreEqual(200, world.Get<TestComponent>(entity).Value);
        }


        public void ApplyAddedComponent()
        {
            entity = world.Create();

            commandBufferSynchronizer.Apply(world, commandBuffer, entity, CRDTReconciliationEffect.ComponentAdded, new TestComponent { Value = 300 });
            commandBuffer.Playback();

            componentPool.DidNotReceive().Release(Arg.Any<TestComponent>());
            Assert.AreEqual(300, world.Get<TestComponent>(entity).Value);
        }


        public void ApplyDeletedComponent()
        {
            entity = world.Create(new TestComponent { Value = 100 }, RemovedComponents.CreateDefault());

            commandBufferSynchronizer.Apply(world, commandBuffer, entity, CRDTReconciliationEffect.ComponentDeleted, null);
            commandBuffer.Playback();

            componentPool.Received(1).Release(Arg.Is<TestComponent>(t => t.Value == 100));
            Assert.IsFalse(world.Has<TestComponent>(entity));
        }
    }
}
