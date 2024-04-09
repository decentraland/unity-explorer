using Arch.CommandBuffer;
using Arch.Core;
using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Serialization;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace CrdtEcsBridge.WorldSynchronizer.Tests
{
    public class WorldSyncCommandBufferShould
    {
        private const int COMPONENT_ID_1 = 100;
        private const int COMPONENT_ID_2 = 200;
        private const int ENTITY_ID = 200;

        // random byte array
        private static readonly CRDTPooledMemoryAllocator CRDT_POOLED_MEMORY_ALLOCATOR = CRDTPooledMemoryAllocator.Create();
        private static readonly byte[] DATA_CONTENT = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

        private static readonly IMemoryOwner<byte> DATA = CRDT_POOLED_MEMORY_ALLOCATOR.GetMemoryBuffer(DATA_CONTENT);

        private ISDKComponentsRegistry sdkComponentsRegistry;
        private WorldSyncCommandBuffer worldSyncCommandBuffer;
        private ISceneEntityFactory entityFactory;
        private WorldSyncCommandBufferCollectionsPool collectionsPool;


        public void SetUp()
        {
            sdkComponentsRegistry = Substitute.For<ISDKComponentsRegistry>();

            sdkComponentsRegistry.TryGet(COMPONENT_ID_1, out Arg.Any<SDKComponentBridge>())
                                 .Returns(x =>
                                  {
                                      IComponentPool<TestComponent> pool = Substitute.For<IComponentPool<TestComponent>>();
                                      pool.Get().Returns(_ => new TestComponent());
                                      pool.Rent().Returns(_ => new TestComponent());

                                      var serializer = new TestComponentSerializer();

                                      x[1] = SDKComponentBuilder<TestComponent>.Create(COMPONENT_ID_1)
                                                                               .WithPool(pool)
                                                                               .WithCustomSerializer(serializer)
                                                                               .Build();

                                      return true;
                                  });

            sdkComponentsRegistry.TryGet(COMPONENT_ID_2, out Arg.Any<SDKComponentBridge>())
                                 .Returns(x =>
                                  {
                                      IComponentPool<TestComponent2> pool = Substitute.For<IComponentPool<TestComponent2>>();
                                      pool.Get().Returns(_ => new TestComponent2());
                                      pool.Rent().Returns(_ => new TestComponent2());

                                      var serializer = new TestComponentSerializer2();

                                      x[1] = SDKComponentBuilder<TestComponent2>.Create(COMPONENT_ID_2)
                                                                                .WithPool(pool)
                                                                                .WithCustomSerializer(serializer)
                                                                                .Build();

                                      return true;
                                  });

            entityFactory = Substitute.For<ISceneEntityFactory>();
            entityFactory.Create(Arg.Any<CRDTEntity>(), Arg.Any<World>()).Returns(c => c.Arg<World>().Create());

            worldSyncCommandBuffer = new WorldSyncCommandBuffer(sdkComponentsRegistry, entityFactory, collectionsPool = WorldSyncCommandBufferCollectionsPool.Create());
        }


        public void TearDown()
        {
            worldSyncCommandBuffer?.Dispose();
            collectionsPool?.Dispose();
        }



        public void MergeReconciledMessagesCorrectly((CRDTMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected)[] series)
        {
            void FillDeserializeLoop(int lastIndex)
            {
                var pool = WorldSyncCommandBufferCollectionsPool.Create();
                var localBuffer = new WorldSyncCommandBuffer(sdkComponentsRegistry, entityFactory, pool);

                try
                {
                    CRDTReconciliationEffect finalExpectation = CRDTReconciliationEffect.NoChanges;

                    for (var i = 0; i <= lastIndex; i++)
                    {
                        (CRDTMessage crdtMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected) = series[i];
                        finalExpectation = expected;
                        localBuffer.SyncCRDTMessage(crdtMessage, effect);
                    }

                    localBuffer.FinalizeAndDeserialize();

                    // Check the final status
                    Assert.AreEqual(finalExpectation, localBuffer.GetLastState(ENTITY_ID, COMPONENT_ID_1), $"The final state mismatch at index {lastIndex}");
                }
                finally
                {
                    localBuffer.Dispose();
                    pool.Dispose();
                }
            }

            for (var i = 0; i < series.Length; i++)
            {
                (CRDTMessage crdtMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected) = series[i];
                CRDTReconciliationEffect result = worldSyncCommandBuffer.SyncCRDTMessage(crdtMessage, effect);

                // check that the last status was correctly updated
                Assert.AreEqual(effect, result, $"Mismatch at index {i}");

                FillDeserializeLoop(i);
            }
        }


        public void FailGracefullyOnUnknownComponent()
        {
            var message = new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, ENTITY_ID, 999, 0, DATA);
            CRDTReconciliationEffect result = worldSyncCommandBuffer.SyncCRDTMessage(message, CRDTReconciliationEffect.ComponentAdded);
            Assert.AreEqual(CRDTReconciliationEffect.NoChanges, result);
        }

        private static (CRDTMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected)[][] MessagesSource()
        {
            return new[]
            {
                new (CRDTMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected)[]
                {
                    // the first message is decisive
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentAdded),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentAdded),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.NoChanges),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentAdded),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentAdded),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentAdded),
                },
                new (CRDTMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected)[]
                {
                    // the first message is decisive
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentDeleted),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified),
                },
                new (CRDTMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected)[]
                {
                    // the first message is decisive
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentDeleted),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified, CRDTReconciliationEffect.ComponentModified),
                    (CreateTestMessage(), CRDTReconciliationEffect.ComponentDeleted, CRDTReconciliationEffect.ComponentDeleted),
                },
                new (CRDTMessage, CRDTReconciliationEffect effect, CRDTReconciliationEffect expected)[]
                {
                    // special case for deleted entity
                    (CreateTestMessage(), CRDTReconciliationEffect.EntityDeleted, CRDTReconciliationEffect.NoChanges), // no changes to the component = no component
                    (new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 123, 0, 123, EmptyMemoryOwner<byte>.EMPTY), CRDTReconciliationEffect.EntityDeleted, CRDTReconciliationEffect.NoChanges),
                },
            };
        }

        private static CRDTMessage CreateTestMessage(int componentId = COMPONENT_ID_1, byte[] data = null) =>

            // type and timestamp do not matter
            new (CRDTMessageType.NONE, ENTITY_ID, COMPONENT_ID_1, 0, CRDT_POOLED_MEMORY_ALLOCATOR.GetMemoryBuffer(data) ?? DATA);

        private static CRDTMessage CreateTestMessage2(byte[] data = null) =>

            // type and timestamp do not matter
            new (CRDTMessageType.NONE, ENTITY_ID, COMPONENT_ID_2, 0, CRDT_POOLED_MEMORY_ALLOCATOR.GetMemoryBuffer(data) ?? DATA);

        private static CRDTMessage CreateDeleteEntityMessage(int entity) =>
            new (CRDTMessageType.DELETE_ENTITY, entity, 0, 0, EmptyMemoryOwner<byte>.EMPTY);



        public void ApplyChangesCorrectly(Action<World, Dictionary<CRDTEntity, Entity>> prewarmWorld, (CRDTMessage, CRDTReconciliationEffect)[] messages, Action<World, Dictionary<CRDTEntity, Entity>> assertWorld)
        {
            var world = World.Create();
            var commandBuffer = new PersistentCommandBuffer(world);
            var collectionPool = WorldSyncCommandBufferCollectionsPool.Create();
            var localBuffer = new WorldSyncCommandBuffer(sdkComponentsRegistry, entityFactory, collectionPool);

            try
            {
                var entitiesMap = new Dictionary<CRDTEntity, Entity>();
                prewarmWorld(world, entitiesMap);

                foreach ((CRDTMessage message, CRDTReconciliationEffect effect) in messages)
                    localBuffer.SyncCRDTMessage(message, effect);

                // deserialize first
                localBuffer.FinalizeAndDeserialize();

                localBuffer.Apply(world, commandBuffer, entitiesMap);

                assertWorld(world, entitiesMap);
            }
            finally
            {
                localBuffer.Dispose();
                collectionPool.Dispose();
            }
        }

        private static object[][] ApplyChangesMessagesSource()
        {
            return new[]
            {
                new object[]
                {
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) => { }),
                    new (CRDTMessage, CRDTReconciliationEffect)[]
                    {
                        (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded),
                    },
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) => { Assert.IsTrue(world.Has<TestComponent>(map[ENTITY_ID])); }),
                },
                new object[]
                {
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) => { }),
                    new[]
                    {
                        (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded),
                        (CreateTestMessage(), CRDTReconciliationEffect.ComponentModified),
                        (CreateTestMessage(), CRDTReconciliationEffect.ComponentDeleted),
                    },

                    // no entity should be created
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) => { Assert.IsFalse(map.ContainsKey(ENTITY_ID)); }),
                },
                new object[]
                {
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) => { }),
                    new (CRDTMessage, CRDTReconciliationEffect)[]
                    {
                        (CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded),
                        (CreateTestMessage2(), CRDTReconciliationEffect.ComponentAdded),
                    },

                    // no entity should be created
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) =>
                    {
                        Assert.IsTrue(world.Has<TestComponent>(map[ENTITY_ID]));
                        Assert.IsTrue(world.Has<TestComponent2>(map[ENTITY_ID]));
                    }),
                },
                new object[]
                {
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) =>
                    {
                        Entity entity = world.Create(new TestComponent { Value = new byte[] { 0, 122, 13, 11 } }, RemovedComponents.CreateDefault());
                        map.Add(ENTITY_ID, entity);
                    }),
                    new (CRDTMessage, CRDTReconciliationEffect)[]
                    {
                        (CreateTestMessage(data: DATA_CONTENT), CRDTReconciliationEffect.ComponentModified),
                    },
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) =>
                    {
                        Assert.IsTrue(world.Has<TestComponent>(map[ENTITY_ID]));
                        TestComponent c = world.Get<TestComponent>(map[ENTITY_ID]);

                        // last data should be written
                        Assert.AreEqual(DATA.Memory.ToArray(), c.Value.ToArray());
                    }),
                },
                new object[]
                {
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) =>
                    {
                        Entity entity = world.Create(new TestComponent { Value = new byte[] { 0, 122, 13, 11 } }, RemovedComponents.CreateDefault());
                        map.Add(ENTITY_ID, entity);
                    }),
                    new (CRDTMessage, CRDTReconciliationEffect)[]
                    {
                        (CreateTestMessage(), CRDTReconciliationEffect.ComponentDeleted),
                    },
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) => { Assert.IsFalse(world.Has<TestComponent>(map[ENTITY_ID])); }),
                },
                new object[]
                {
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) =>
                    {
                        Entity entity = world.Create(CreateTestMessage(data: new byte[] { 127, 126, 123 }), RemovedComponents.CreateDefault());
                        map.Add(ENTITY_ID, entity);
                    }),
                    new (CRDTMessage, CRDTReconciliationEffect)[]
                    {
                        (CreateDeleteEntityMessage(ENTITY_ID), CRDTReconciliationEffect.EntityDeleted),
                    },
                    new Action<World, Dictionary<CRDTEntity, Entity>>((world, map) =>
                    {
                        Assert.IsFalse(map.ContainsKey(ENTITY_ID));

                        // The entity was deleted, check that all entities has DeleteEntityIntention component
                        QueryDescription q = new QueryDescription().WithAll<DeleteEntityIntention>();

                        Assert.AreEqual(1, world.CountEntities(in q));
                    }),
                },
            };
        }


        public void ThrowIfFinalized()
        {
            worldSyncCommandBuffer.SyncCRDTMessage(CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded);
            worldSyncCommandBuffer.FinalizeAndDeserialize();

            var world = World.Create();
            var commandBuffer = new PersistentCommandBuffer(world);

            var entitiesMap = new Dictionary<CRDTEntity, Entity>();

            worldSyncCommandBuffer.Apply(world, commandBuffer, entitiesMap);

            Assert.Throws<InvalidOperationException>(() => worldSyncCommandBuffer.SyncCRDTMessage(CreateTestMessage(), CRDTReconciliationEffect.ComponentModified));
        }


        public void ThrowIfApplyCalledBeforeDeserialize()
        {
            worldSyncCommandBuffer.SyncCRDTMessage(CreateTestMessage(), CRDTReconciliationEffect.ComponentAdded);

            var world = World.Create();
            var commandBuffer = new PersistentCommandBuffer(world);

            var entitiesMap = new Dictionary<CRDTEntity, Entity>();

            Assert.Throws<InvalidOperationException>(() => worldSyncCommandBuffer.Apply(world, commandBuffer, entitiesMap));
        }

        public class TestComponent
        {
            public byte[] Value;
        }

        public class TestComponent2
        {
            public byte[] Value;
        }

        public class TestComponentSerializer : IComponentSerializer<TestComponent>
        {
            public void DeserializeInto(TestComponent instance, in ReadOnlySpan<byte> data)
            {
                instance.Value = data.ToArray();
            }

            public void SerializeInto(TestComponent model, in Span<byte> span) { }
        }

        public class TestComponentSerializer2 : IComponentSerializer<TestComponent2>
        {
            public void DeserializeInto(TestComponent2 instance, in ReadOnlySpan<byte> data)
            {
                instance.Value = data.ToArray();
            }

            public void SerializeInto(TestComponent2 model, in Span<byte> span) { }
        }
    }
}
