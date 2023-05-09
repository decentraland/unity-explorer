using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.WorldSynchronizer;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CrdtEcsBridge.Engine.Tests
{
    public class EngineAPIImplementationShould
    {
        private class CRDTSerializer : ICRDTSerializer
        {
            public void Serialize(ref Span<byte> destination, in ProcessedCRDTMessage processedMessage) { }
        }

        private static readonly byte[] OUTPUT = { 10, 20, 30, 20, 10, 0 };
        private static readonly byte[] INPUT = { 0, 3, 5, 7, 10, 19, 20, 40, 76 };

        private IEngineAPIPoolsProvider engineAPIPoolsProvider;
        private ICRDTProtocol crdtProtocol;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ICRDTWorldSynchronizer crdtWorldSynchronizer;
        private IOutgoingCRTDMessagesProvider outgoingCrtdMessagesProvider;

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

            crdtMessages = new List<CRDTMessage>
            {
                new (CRDTMessageType.PUT_COMPONENT, 10, 100, 1, ReadOnlyMemory<byte>.Empty),
                new (CRDTMessageType.APPEND_COMPONENT, 10, 123, 1, ReadOnlyMemory<byte>.Empty),
                new (CRDTMessageType.DELETE_ENTITY, 12, 0, 0, ReadOnlyMemory<byte>.Empty),
            };

            outgoingMessages = new List<ProcessedCRDTMessage>
            {
                new (new (CRDTMessageType.APPEND_COMPONENT, 122, 100, 1, new byte[100]), 120)
            };

            crdtStateMessages = new List<ProcessedCRDTMessage>
            {
                new (new (CRDTMessageType.APPEND_COMPONENT, 33, 33, 1, new byte[100]), 120),
                new (new (CRDTMessageType.APPEND_COMPONENT, 44, 33, 1, new byte[23]), 130),
                new (new (CRDTMessageType.PUT_COMPONENT, 122, 33, 1, new byte[33]), 140),
                new (new (CRDTMessageType.PUT_COMPONENT, 122, 1000, 1, new byte[44]), 10),
            };

            engineAPIImplementation = new EngineAPIImplementation(
                engineAPIPoolsProvider = Substitute.For<IEngineAPIPoolsProvider>(),
                crdtProtocol = Substitute.For<ICRDTProtocol>(),
                crdtDeserializer = Substitute.For<ICRDTDeserializer>(),
                crdtSerializer = new CRDTSerializer(),
                crdtWorldSynchronizer = Substitute.For<ICRDTWorldSynchronizer>(),
                outgoingCrtdMessagesProvider = Substitute.For<IOutgoingCRTDMessagesProvider>()
            );

            crdtDeserializer.When(d => d.DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>()))
                            .Do(c =>
                             {
                                 var list = c.ArgAt<IList<CRDTMessage>>(1);

                                 foreach (var message in crdtMessages)
                                     list.Add(message);
                             });

            crdtProtocol.ProcessMessage(Arg.Any<CRDTMessage>()).Returns(_ => new CRDTReconciliationResult(CRDTStateReconciliationResult.StateUpdatedData, CRDTReconciliationEffect.ComponentModified));

            crdtProtocol.CreateMessagesFromTheCurrentState(Arg.Any<ProcessedCRDTMessage[]>())
                        .Returns(c =>
                         {
                             crdtStateMessages.CopyTo(c.ArgAt<ProcessedCRDTMessage[]>(0));
                             return crdtStateMessages.Aggregate(0, (acc, message) => acc + message.CRDTMessageDataLength);
                         });

            crdtProtocol.GetMessagesCount().Returns(crdtStateMessages.Count);

            crdtWorldSynchronizer.GetSyncCommandBuffer().Returns(_ => worldSyncCommandBuffer = Substitute.For<IWorldSyncCommandBuffer>());

            engineAPIPoolsProvider.GetDeserializationMessagesPool().Returns(_ => new List<CRDTMessage>());
            engineAPIPoolsProvider.GetSerializedStateBytesPool(Arg.Any<int>()).Returns(c => new byte[c.Arg<int>()]);
            engineAPIPoolsProvider.GetSerializationCrdtMessagesPool(Arg.Any<int>()).Returns(c => new ProcessedCRDTMessage[c.Arg<int>()]);

            outgoingCrtdMessagesProvider.GetSerializationSyncBlock()
                                        .Returns(_ =>
                                         {
                                             mutex.WaitOne();
                                             return new OutgoingCRDTMessagesSyncBlock(outgoingMessages, mutex);
                                         });
        }

        [Test]
        public async Task CallDeserializeBatch()
        {
            await engineAPIImplementation.CrdtSendToRenderer(INPUT);

            crdtDeserializer.Received(1).DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>());
        }

        [Test]
        public async Task MakeCallsInProperOrderCrdtSendToRenderer()
        {
            await engineAPIImplementation.CrdtSendToRenderer(INPUT);

            Received.InOrder(() =>
            {
                engineAPIPoolsProvider.GetDeserializationMessagesPool();
                crdtDeserializer.DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>());
                crdtWorldSynchronizer.GetSyncCommandBuffer();

                for (var i = 0; i < crdtMessages.Count; i++)
                {
                    CRDTMessage crdtMessage = crdtMessages[i];
                    crdtProtocol.ProcessMessage(crdtMessage);
                    worldSyncCommandBuffer.SyncCRDTMessage(in crdtMessage, CRDTReconciliationEffect.ComponentModified);
                }

                worldSyncCommandBuffer.FinalizeAndDeserialize();
                engineAPIPoolsProvider.ReleaseDeserializationMessagesPool(Arg.Any<IList<CRDTMessage>>());

                outgoingCrtdMessagesProvider.GetSerializationSyncBlock();
                engineAPIPoolsProvider.GetSerializedStateBytesPool(Arg.Any<int>());

                // can't check Serialize, can't mock `Span<byte>`
                // Apply will be called asynchronously
                // crdtWorldSynchronizer.Received(1).ApplySyncCommandBuffer(worldSyncCommandBuffer);
            });
        }

        [Test]
        public async Task MakeCallsInProperOrderCrdtGetState()
        {
            var state = await engineAPIImplementation.CrdtGetState();

            Received.InOrder(() =>
            {
                crdtProtocol.GetMessagesCount();
                engineAPIPoolsProvider.GetSerializationCrdtMessagesPool(crdtStateMessages.Count);
                crdtProtocol.CreateMessagesFromTheCurrentState(Arg.Any<ProcessedCRDTMessage[]>());
                outgoingCrtdMessagesProvider.GetSerializationSyncBlock();
                engineAPIPoolsProvider.GetSerializedStateBytesPool(400 + 120);
                engineAPIPoolsProvider.ReleaseSerializationCrdtMessagesPool(Arg.Is<ProcessedCRDTMessage[]>(p => p.Length == crdtStateMessages.Count));
            });

            Assert.AreEqual(400 + 120, state.Length);
        }

        [Test]
        public async Task ReleasePreviousBufferOnSend()
        {
            await engineAPIImplementation.CrdtSendToRenderer(INPUT);
            await engineAPIImplementation.CrdtSendToRenderer(INPUT);

            engineAPIPoolsProvider.Received(1).ReleaseSerializedStateBytesPool(Arg.Any<byte[]>());
        }

        [Test]
        public async Task ReleasePreviousBufferOnGet()
        {
            await engineAPIImplementation.CrdtSendToRenderer(INPUT);
            await engineAPIImplementation.CrdtGetState();

            engineAPIPoolsProvider.Received(1).ReleaseSerializedStateBytesPool(Arg.Any<byte[]>());
        }
    }
}
