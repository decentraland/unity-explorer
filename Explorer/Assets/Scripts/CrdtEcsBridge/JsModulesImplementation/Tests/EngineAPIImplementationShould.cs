using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    public class EngineAPIImplementationShould
    {
        private static readonly byte[] OUTPUT = { 10, 20, 30, 20, 10, 0 };
        private static readonly byte[] INPUT = { 0, 3, 5, 7, 10, 19, 20, 40, 76 };

        private ISharedPoolsProvider sharedPoolsProvider;
        private IInstancePoolsProvider instancePoolsProvider;
        private ICRDTProtocol crdtProtocol;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ICRDTWorldSynchronizer crdtWorldSynchronizer;
        private IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider;
        private CRDTPooledMemoryAllocator crdtPooledMemoryAllocator;

        private EngineAPIImplementation engineAPIImplementation;

        private List<CRDTMessage> crdtMessages;
        private List<ProcessedCRDTMessage> outgoingMessages;
        private List<ProcessedCRDTMessage> crdtStateMessages;

        private IWorldSyncCommandBuffer worldSyncCommandBuffer;
        private Mutex mutex;

        [SetUp]
        public void SetUp()
        {
            mutex = new Mutex();

            crdtPooledMemoryAllocator = CRDTPooledMemoryAllocator.Create();

            crdtMessages = new List<CRDTMessage>
            {
                new (CRDTMessageType.PUT_COMPONENT, 10, 100, 1, EmptyMemoryOwner<byte>.EMPTY),
                new (CRDTMessageType.APPEND_COMPONENT, 10, 123, 1, EmptyMemoryOwner<byte>.EMPTY),
                new (CRDTMessageType.DELETE_ENTITY, 12, 0, 0, EmptyMemoryOwner<byte>.EMPTY),
            };

            outgoingMessages = new List<ProcessedCRDTMessage>
            {
                new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 122, 100, 1, crdtPooledMemoryAllocator.GetMemoryBuffer(new byte[100])), 120),
            };

            crdtStateMessages = new List<ProcessedCRDTMessage>
            {
                new (new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 33, 33, 1, crdtPooledMemoryAllocator.GetMemoryBuffer(new byte[100])), 120),
                new (new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 44, 33, 1, crdtPooledMemoryAllocator.GetMemoryBuffer(new byte[23])), 130),
                new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 122, 33, 1, crdtPooledMemoryAllocator.GetMemoryBuffer(new byte[33])), 140),
                new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 122, 1000, 1, crdtPooledMemoryAllocator.GetMemoryBuffer(new byte[44])), 10),
            };

            engineAPIImplementation = new EngineAPIImplementation(
                sharedPoolsProvider = Substitute.For<ISharedPoolsProvider>(), instancePoolsProvider = Substitute.For<IInstancePoolsProvider>(),
                crdtProtocol = Substitute.For<ICRDTProtocol>(),
                crdtDeserializer = Substitute.For<ICRDTDeserializer>(),
                crdtSerializer = new CRDTSerializer(),
                crdtWorldSynchronizer = Substitute.For<ICRDTWorldSynchronizer>(),
                outgoingCrtdMessagesProvider = Substitute.For<IOutgoingCRDTMessagesProvider>(),
                Substitute.For<ISystemGroupsUpdateGate>(),
                new RethrowSceneExceptionsHandler(),
                new MutexSync()
            );

            crdtDeserializer.When(d => d.DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>()))
                            .Do(c =>
                             {
                                 IList<CRDTMessage> list = c.ArgAt<IList<CRDTMessage>>(1);

                                 foreach (CRDTMessage message in crdtMessages)
                                     list.Add(message);
                             });

            crdtProtocol.ProcessMessage(Arg.Any<CRDTMessage>()).Returns(_ => new CRDTReconciliationResult(CRDTStateReconciliationResult.StateUpdatedData, CRDTReconciliationEffect.ComponentModified));

            crdtProtocol.CreateMessagesFromTheCurrentState(Arg.Any<ProcessedCRDTMessage[]>())
                        .Returns(c =>
                         {
                             ProcessedCRDTMessage[] processedMessages = c.ArgAt<ProcessedCRDTMessage[]>(0);

                             crdtStateMessages.CopyTo(processedMessages);
                             outgoingMessages.CopyTo(processedMessages, crdtStateMessages.Count);
                             return crdtStateMessages.Concat(outgoingMessages).Aggregate(0, (acc, message) => acc + message.CRDTMessageDataLength);
                         });

            crdtProtocol.GetMessagesCount().Returns(crdtStateMessages.Count + outgoingMessages.Count);

            crdtWorldSynchronizer.GetSyncCommandBuffer().Returns(_ => worldSyncCommandBuffer = Substitute.For<IWorldSyncCommandBuffer>());

            instancePoolsProvider.GetDeserializationMessagesPool().Returns(_ => new List<CRDTMessage>());
            sharedPoolsProvider.GetSerializedStateBytesPool(Arg.Any<int>()).Returns(c => new PoolableByteArray(new byte[c.Arg<int>()], c.Arg<int>(), sharedPoolsProvider.ReleaseSerializedStateBytesPool));
            sharedPoolsProvider.GetSerializationCrdtMessagesPool(Arg.Any<int>()).Returns(c => new ProcessedCRDTMessage[c.Arg<int>()]);

            outgoingCrtdMessagesProvider.GetSerializationSyncBlock()
                                        .Returns(_ =>
                                         {
                                             mutex.WaitOne();
                                             return new OutgoingCRDTMessagesSyncBlock(outgoingMessages.ToList());
                                         });
        }

        [Test]
        public void CallDeserializeBatch()
        {
            engineAPIImplementation.CrdtSendToRenderer(INPUT);

            crdtDeserializer.Received(1).DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>());
        }

        [Test]
        public void MakeCallsInProperOrderCrdtSendToRenderer()
        {
            engineAPIImplementation.CrdtSendToRenderer(INPUT);

            Received.InOrder(() =>
            {
                instancePoolsProvider.GetDeserializationMessagesPool();
                crdtDeserializer.DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>());
                crdtWorldSynchronizer.GetSyncCommandBuffer();

                for (var i = 0; i < crdtMessages.Count; i++)
                {
                    CRDTMessage crdtMessage = crdtMessages[i];
                    crdtProtocol.ProcessMessage(crdtMessage);
                    worldSyncCommandBuffer.SyncCRDTMessage(in crdtMessage, CRDTReconciliationEffect.ComponentModified);
                }

                worldSyncCommandBuffer.FinalizeAndDeserialize();
                instancePoolsProvider.ReleaseDeserializationMessagesPool(Arg.Any<List<CRDTMessage>>());

                outgoingCrtdMessagesProvider.GetSerializationSyncBlock();
                sharedPoolsProvider.GetSerializedStateBytesPool(Arg.Any<int>());

                // can't check Serialize, can't mock `Span<byte>`
                // Apply will be called asynchronously
                // crdtWorldSynchronizer.Received(1).ApplySyncCommandBuffer(worldSyncCommandBuffer);
            });
        }

        [Test]
        public void MakeCallsInProperOrderCrdtGetState()
        {
            PoolableByteArray state = engineAPIImplementation.CrdtGetState();

            Received.InOrder(() =>
            {
                outgoingCrtdMessagesProvider.GetSerializationSyncBlock();
                crdtProtocol.GetMessagesCount();
                sharedPoolsProvider.GetSerializationCrdtMessagesPool(crdtStateMessages.Count + outgoingMessages.Count);
                crdtProtocol.CreateMessagesFromTheCurrentState(Arg.Any<ProcessedCRDTMessage[]>());
                sharedPoolsProvider.GetSerializedStateBytesPool(400 + 120);
                sharedPoolsProvider.ReleaseSerializationCrdtMessagesPool(Arg.Is<ProcessedCRDTMessage[]>(p => p.Length == crdtStateMessages.Count + outgoingMessages.Count));
            });

            Assert.AreEqual(400 + 120, state.Length);
        }

        [Test]
        public void ReleasePreviousBufferCrdtSendToRenderer()
        {
            PoolableByteArray data = engineAPIImplementation.CrdtSendToRenderer(INPUT);
            data.Dispose();

            sharedPoolsProvider.Received(1).ReleaseSerializedStateBytesPool(Arg.Any<byte[]>());
        }

        [Test]
        public void ReleasePreviousBufferCrdtGetState()
        {
            PoolableByteArray data = engineAPIImplementation.CrdtGetState();
            data.Dispose();

            sharedPoolsProvider.Received(1).ReleaseSerializedStateBytesPool(Arg.Any<byte[]>());
        }

        private class CRDTSerializer : ICRDTSerializer
        {
            public void Serialize(ref Span<byte> destination, in ProcessedCRDTMessage processedMessage) { }
        }
    }
}
