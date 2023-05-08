using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CrdtEcsBridge.OutgoingMessages.Tests
{
    public class OutgoingCRDTMessagesSyncBlockShould
    {
        private OutgoingCRDTMessagesSyncBlock syncBlock;

        private List<ProcessedCRDTMessage> messages;
        private Mutex mutex;

        [SetUp]
        public void SetUp()
        {
            syncBlock = new OutgoingCRDTMessagesSyncBlock(
                messages = new List<ProcessedCRDTMessage>
                {
                    new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 100, 100, 0, EmptyMemoryOwner<byte>.EMPTY), 30),
                    new (new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 123, 0, 0, EmptyMemoryOwner<byte>.EMPTY), 60),
                },
                mutex = new Mutex()
            );

            mutex.WaitOne();
        }

        [TearDown]
        public void TearDown()
        {
            mutex.Dispose();
        }

        [Test]
        public void GetPayloadLength()
        {
            Assert.AreEqual(90, syncBlock.GetPayloadLength());
        }

        [Test]
        public void ReleaseMutexOnDispose()
        {
            syncBlock.Dispose();
            Assert.IsTrue(mutex.WaitOne(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void ClearMessagesOnDispose()
        {
            syncBlock.Dispose();
            CollectionAssert.IsEmpty(messages);
        }
    }
}
