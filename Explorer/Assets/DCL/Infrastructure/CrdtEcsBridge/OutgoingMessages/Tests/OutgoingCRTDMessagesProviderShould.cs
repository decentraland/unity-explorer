using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.Components;
using DCL.ECS7;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System.Buffers;

namespace CrdtEcsBridge.OutgoingMessages.Tests
{
    public class OutgoingCRTDMessagesProviderShould
    {
        private OutgoingCRDTMessagesProvider provider;
        private ICRDTProtocol crdtProtocol;
        private SDKComponentsRegistry sdkComponentRegistry;

        [SetUp]
        public void SetUp()
        {
            crdtProtocol = Substitute.For<ICRDTProtocol>();
            sdkComponentRegistry = new SDKComponentsRegistry();
            provider = new OutgoingCRDTMessagesProvider(sdkComponentRegistry, crdtProtocol, CRDTPooledMemoryAllocator.Create());
        }

        [Test]
        public void AppendMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());
            var crdtEntity = new CRDTEntity(1);

            //Act
            PBPointerEventsResult message = provider.AppendMessage<PBPointerEventsResult, object>(
                (_, _) => { }, crdtEntity, 100, null);

            //Assert
            OutgoingCRDTMessagesProvider.PendingMessage addedMessage = provider.messages[0];

            Assert.That(addedMessage.MessageType, Is.EqualTo(CRDTMessageType.APPEND_COMPONENT));
            Assert.That(addedMessage.Message, Is.EqualTo(message));
            Assert.That(addedMessage.Entity, Is.EqualTo(crdtEntity));
            Assert.That(addedMessage.Bridge.Id, Is.EqualTo(ComponentID.POINTER_EVENTS_RESULT));
            Assert.That(addedMessage.Timestamp, Is.EqualTo(100));

            Assert.That(provider.lwwMessageIndices, Does.Not.ContainKey(new OutgoingMessageKey(crdtEntity, ComponentID.POINTER_EVENTS_RESULT)));
        }

        [Test]
        public void PutMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());
            var crdtEntity = new CRDTEntity(1);

            //Act
            PBPointerEventsResult message = provider.AddPutMessage<PBPointerEventsResult, int>(
                (_, _) => { }, crdtEntity, 100);

            //Assert
            OutgoingCRDTMessagesProvider.PendingMessage addedMessage = provider.messages[0];

            Assert.That(addedMessage.MessageType, Is.EqualTo(CRDTMessageType.PUT_COMPONENT));
            Assert.That(addedMessage.Message, Is.EqualTo(message));
            Assert.That(addedMessage.Entity, Is.EqualTo(crdtEntity));
            Assert.That(addedMessage.Bridge.Id, Is.EqualTo(ComponentID.POINTER_EVENTS_RESULT));
            Assert.That(addedMessage.Timestamp, Is.EqualTo(0));

            Assert.That(provider.lwwMessageIndices, Contains.Key(new OutgoingMessageKey(crdtEntity, ComponentID.POINTER_EVENTS_RESULT)));
        }

        [Test]
        public void DeleteMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());
            var crdtEntity = new CRDTEntity(1);

            provider.AddDeleteMessage<PBPointerEventsResult>(crdtEntity);

            OutgoingCRDTMessagesProvider.PendingMessage addedMessage = provider.messages[0];

            Assert.That(addedMessage.MessageType, Is.EqualTo(CRDTMessageType.DELETE_COMPONENT));
            Assert.That(addedMessage.Message, Is.Null);
            Assert.That(addedMessage.Entity, Is.EqualTo(crdtEntity));
            Assert.That(addedMessage.Bridge.Id, Is.EqualTo(ComponentID.POINTER_EVENTS_RESULT));
            Assert.That(addedMessage.Timestamp, Is.EqualTo(0));

            Assert.That(provider.lwwMessageIndices, Contains.Key(new OutgoingMessageKey(crdtEntity, ComponentID.POINTER_EVENTS_RESULT)));
        }

        [Test]
        public void ResolveSeriesOfLwwMessages()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            var crdtEntity = new CRDTEntity(1);

            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 100);
            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 200);
            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 300);
            PBPointerEventsResult lastMes = provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 400);

            Assert.That(provider.messages, Has.Count.EqualTo(1));
            Assert.That(provider.messages[0].Message, Is.EqualTo(lastMes));
            Assert.That(provider.messages[0].Timestamp, Is.EqualTo(0));
            Assert.That(provider.messages[0].MessageType, Is.EqualTo(CRDTMessageType.PUT_COMPONENT));
        }

        [Test]
        public void ResolveSeriesOfLwwMessagesWithDeleteMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            var crdtEntity = new CRDTEntity(1);

            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 100);
            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 200);
            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, crdtEntity, 300);
            provider.AddDeleteMessage<PBPointerEventsResult>(crdtEntity);

            Assert.That(provider.messages, Has.Count.EqualTo(1));
            Assert.That(provider.messages[0].Message, Is.Null);
            Assert.That(provider.messages[0].Timestamp, Is.EqualTo(0));
            Assert.That(provider.messages[0].MessageType, Is.EqualTo(CRDTMessageType.DELETE_COMPONENT));
        }

        [Test]
        public void ProvideSerializationSyncBlock()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, new CRDTEntity(1), 100);
            provider.AddPutMessage<PBPointerEventsResult, int>((_, _) => { }, new CRDTEntity(2), 100);
            provider.AppendMessage<PBPointerEventsResult, int>((_, _) => { }, new CRDTEntity(3), 780, 100);

            using OutgoingCRDTMessagesSyncBlock syncBlock = provider.GetSerializationSyncBlock(null);

            crdtProtocol.Received(1).CreatePutMessage(Arg.Is<CRDTEntity>(c => c.Id == 1), ComponentID.POINTER_EVENTS_RESULT, Arg.Any<IMemoryOwner<byte>>());
            crdtProtocol.Received(1).CreatePutMessage(Arg.Is<CRDTEntity>(c => c.Id == 2), ComponentID.POINTER_EVENTS_RESULT, Arg.Any<IMemoryOwner<byte>>());
            crdtProtocol.Received(1).CreateAppendMessage(Arg.Is<CRDTEntity>(c => c.Id == 3), ComponentID.POINTER_EVENTS_RESULT, 780, Arg.Any<IMemoryOwner<byte>>());
        }

        [Test]
        public void ReleaseToPoolOnDispose()
        {
            provider.Dispose();

            Assert.GreaterOrEqual(OutgoingCRDTMessagesProvider.MESSAGES_SHARED_POOL.CountInactive, 1);
            Assert.GreaterOrEqual(OutgoingCRDTMessagesProvider.INDICES_SHARED_POOL.CountInactive, 1);
        }
    }
}
