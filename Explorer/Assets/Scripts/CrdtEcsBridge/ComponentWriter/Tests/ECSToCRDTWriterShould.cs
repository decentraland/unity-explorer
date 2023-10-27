using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.OutgoingMessages;
using DCL.ECS7;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System.Buffers;

namespace CrdtEcsBridge.ECSToCRDTWriter.Tests
{
    [TestFixture]
    public class ECSToCRDTWriterShould
    {
        [SetUp]
        public void Setup()
        {
            crdtProtocol = Substitute.For<ICRDTProtocol>();
            outgoingCRDTMessageProvider = Substitute.For<IOutgoingCRDTMessagesProvider>();
            sdkComponentRegistry = new SDKComponentsRegistry();
            writer = new ComponentWriter.ECSToCRDTWriter(crdtProtocol, outgoingCRDTMessageProvider, sdkComponentRegistry, CRDTPooledMemoryAllocator.Create());
        }

        private ICRDTProtocol crdtProtocol;
        private IOutgoingCRDTMessagesProvider outgoingCRDTMessageProvider;
        private SDKComponentsRegistry sdkComponentRegistry;
        private ComponentWriter.ECSToCRDTWriter writer;

        [Test]
        public void AppendMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());
            var crdtEntity = new CRDTEntity(1);

            //Act
            writer.AppendMessage(crdtEntity, new PBPointerEventsResult(), 100);

            //Assert
            crdtProtocol.Received().CreateAppendMessage(crdtEntity, ComponentID.POINTER_EVENTS_RESULT, 100, Arg.Any<IMemoryOwner<byte>>());
            outgoingCRDTMessageProvider.Received().AppendMessage(Arg.Any<ProcessedCRDTMessage>());
        }

        [Test]
        public void PutLwwMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            //Act
            writer.PutMessage(300, new PBPointerEventsResult());
            outgoingCRDTMessageProvider.Received().AddLwwMessage(Arg.Any<ProcessedCRDTMessage>());
        }

        [Test]
        public void DeleteMessage()
        {
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            writer.PutMessage(50, new PBPointerEventsResult());
            outgoingCRDTMessageProvider.Received().AddLwwMessage(Arg.Any<ProcessedCRDTMessage>());
        }
    }
}
