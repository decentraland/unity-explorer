using CRDT;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.Serialization;
using DCL.ECS7;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.ECSToCRDTWriter.Tests
{
    [TestFixture]
    public class ECSToCRDTWriterShould
    {
        [Test]
        public void AppendMessageShould()
        {
            //Arrange
            ICRDTProtocol crdtProtocol = Substitute.For<ICRDTProtocol>();
            IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider = Substitute.For<IOutgoingCRTDMessagesProvider>();
            var ecsToCRDTWriter = new ECSToCRDTWriter(crdtProtocol, outgoingCRDTMessageProvider);
            ecsToCRDTWriter.RegisterSerializer(ComponentID.POINTER_EVENTS_RESULT, new ProtobufSerializer<PBPointerEventsResult>());
            crdtProtocol.ProcessMessage(Arg.Any<CRDTMessage>()).Returns(_ => new CRDTReconciliationResult(CRDTStateReconciliationResult.StateAppendedData, CRDTReconciliationEffect.ComponentAdded));
            var crdtEntity = new CRDTEntity(1);

            //Act
            ecsToCRDTWriter.AppendMessage(crdtEntity, ComponentID.POINTER_EVENTS_RESULT, new PBPointerEventsResult());

            //Assert
            crdtProtocol.Received().CreateAppendMessage(crdtEntity, Arg.Any<int>(), Arg.Any<ReadOnlyMemory<byte>>());
            outgoingCRDTMessageProvider.Received().AddMessage(Arg.Any<ProcessedCRDTMessage>());
        }

        [Test]
        public void ExceptionThrownIfSerializerNotPresent()
        {
            //Arrange
            ICRDTProtocol crdtProtocol = new CRDTProtocol();
            IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider = Substitute.For<IOutgoingCRTDMessagesProvider>();
            var ecsToCRDTWriter = new ECSToCRDTWriter(crdtProtocol, outgoingCRDTMessageProvider);
            var crdtEntity = new CRDTEntity();

            //Assert
            Assert.Throws<Exception>(() => ecsToCRDTWriter.PutMessage(crdtEntity, ComponentID.POINTER_EVENTS_RESULT, new PBPointerEventsResult()));
        }
    }


}
