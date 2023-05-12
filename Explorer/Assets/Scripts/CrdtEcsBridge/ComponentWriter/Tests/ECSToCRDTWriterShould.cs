using CRDT;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.OutgoingMessages;
using DCL.ECS7;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Buffers;

namespace CrdtEcsBridge.ECSToCRDTWriter.Tests
{
    [TestFixture]
    public class ECSToCRDTWriterShould
    {

        [Test]
        public void AppendMessage()
        {
            //Arrange
            ICRDTProtocol crdtProtocol = Substitute.For<ICRDTProtocol>();
            IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider = Substitute.For<IOutgoingCRTDMessagesProvider>();

            var sdkComponentRegistry = new SDKComponentsRegistry();
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            var ecsToCRDTWriter = new ECSToCRDTWriter(crdtProtocol, outgoingCRDTMessageProvider, sdkComponentRegistry);
            crdtProtocol.ProcessMessage(Arg.Any<CRDTMessage>()).Returns(_ => new CRDTReconciliationResult(CRDTStateReconciliationResult.StateAppendedData, CRDTReconciliationEffect.ComponentAdded));
            var crdtEntity = new CRDTEntity(1);

            //Act
            ecsToCRDTWriter.AppendMessage(crdtEntity, ComponentID.POINTER_EVENTS_RESULT, new PBPointerEventsResult());

            //Assert
            crdtProtocol.Received().CreateAppendMessage(crdtEntity, Arg.Any<int>(), Arg.Any<IMemoryOwner<byte>>());
            outgoingCRDTMessageProvider.Received().AddMessage(Arg.Any<ProcessedCRDTMessage>());
        }

        [Test]
        public void AddMessageToProtocol()
        {
            //Arrange
            ICRDTProtocol crdtProtocol = new CRDTProtocol();
            IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider = Substitute.For<IOutgoingCRTDMessagesProvider>();

            var sdkComponentRegistry = new SDKComponentsRegistry();
            sdkComponentRegistry.Add(SDKComponentBuilder<PBPointerEventsResult>.Create(ComponentID.POINTER_EVENTS_RESULT).AsProtobufComponent());

            var ecsToCRDTWriter = new ECSToCRDTWriter(crdtProtocol, outgoingCRDTMessageProvider, sdkComponentRegistry);

            //Act
            ecsToCRDTWriter.PutMessage(new CRDTEntity(), ComponentID.POINTER_EVENTS_RESULT, new PBPointerEventsResult());

            //Assert
            Assert.AreEqual(crdtProtocol.GetMessagesCount(), 1);
        }

    }


}
