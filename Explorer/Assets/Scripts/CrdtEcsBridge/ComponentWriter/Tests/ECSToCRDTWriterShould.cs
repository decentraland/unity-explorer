using CRDT;
using CrdtEcsBridge.OutgoingMessages;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.ECSToCRDTWriter.Tests
{

    public class ECSToCRDTWriterShould
    {

        public void Setup()
        {
            outgoingCRDTMessageProvider = Substitute.For<IOutgoingCRDTMessagesProvider>();
            writer = new ComponentWriter.ECSToCRDTWriter(outgoingCRDTMessageProvider);
        }

        private IOutgoingCRDTMessagesProvider outgoingCRDTMessageProvider;
        private ComponentWriter.ECSToCRDTWriter writer;


        public void AppendMessage()
        {
            //Act
            writer.AppendMessage<PBPointerEvents, int>((_, _) => { }, new CRDTEntity(500), 100, 5);

            //Assert
            outgoingCRDTMessageProvider.Received().AppendMessage(Arg.Any<Action<PBPointerEvents, int>>(), new CRDTEntity(500), 100, 5);
        }


        public void PutLwwMessage()
        {
            writer.PutMessage<PBPointerEvents, int>((_, _) => { }, new CRDTEntity(500), 100);

            outgoingCRDTMessageProvider.Received().AddPutMessage(Arg.Any<Action<PBPointerEvents, int>>(), new CRDTEntity(500), 100);
        }


        public void DeleteMessage()
        {
            writer.DeleteMessage<PBPointerEvents>(new CRDTEntity(500));

            outgoingCRDTMessageProvider.Received().AddDeleteMessage<PBPointerEvents>(500);
        }
    }
}
