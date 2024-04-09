using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using NUnit.Framework;
using System.Collections.Generic;

namespace CrdtEcsBridge.OutgoingMessages.Tests
{
    public class OutgoingCRDTMessagesSyncBlockShould
    {
        private OutgoingCRDTMessagesSyncBlock syncBlock;

        private List<ProcessedCRDTMessage> messages;


        public void SetUp()
        {
            syncBlock = new OutgoingCRDTMessagesSyncBlock(
                messages = new List<ProcessedCRDTMessage>
                {
                    new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 100, 100, 0, EmptyMemoryOwner<byte>.EMPTY), 30),
                    new (new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 123, 0, 0, EmptyMemoryOwner<byte>.EMPTY), 60),
                }
            );
        }


        public void GetPayloadLength()
        {
            Assert.AreEqual(90, syncBlock.PayloadLength);
        }


        public void ClearMessagesOnDispose()
        {
            syncBlock.Dispose();
            CollectionAssert.IsEmpty(messages);
        }
    }
}
