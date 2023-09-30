using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CrdtEcsBridge.OutgoingMessages.Tests
{
    public class OutgoingCRTDMessagesProviderShould
    {
        private OutgoingCRDTMessagesProvider provider;

        [SetUp]
        public void SetUp()
        {
            provider = new OutgoingCRDTMessagesProvider();
        }

        [Test]
        public void AddMessage()
        {
            var messages = new List<ProcessedCRDTMessage>
            {
                new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 50),
                new (new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 12),
                new (new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 34),
                new (new CRDTMessage(CRDTMessageType.DELETE_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 77),
            };

            for (var i = 0; i < messages.Count; i++) { provider.AppendMessage(messages[i]); }

            CollectionAssert.AreEqual(messages, provider.messages);
        }

        [Test]
        public void AddWaitForMutexRelease()
        {
            var messages = new List<ProcessedCRDTMessage>
            {
                new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 50),
                new (new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 12),
                new (new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 34),
                new (new CRDTMessage(CRDTMessageType.DELETE_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 77),
            };

            void ThrottleInBackgroundThread()
            {
                using var s = provider.GetSerializationSyncBlock();
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            var watch = new Stopwatch();
            watch.Start();

            ThreadPool.QueueUserWorkItem(_ => ThrottleInBackgroundThread());

            Thread.Sleep(100);

            for (var i = 0; i < messages.Count; i++) { provider.AppendMessage(messages[i]); }

            watch.Stop();

            Assert.GreaterOrEqual(watch.Elapsed.TotalSeconds, 1);
        }

        [Test]
        public void ReleaseToPoolOnDispose()
        {
            var messages = new List<ProcessedCRDTMessage>
            {
                new (new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 50),
                new (new CRDTMessage(CRDTMessageType.DELETE_ENTITY, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 12),
                new (new CRDTMessage(CRDTMessageType.APPEND_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 34),
                new (new CRDTMessage(CRDTMessageType.DELETE_COMPONENT, 10, 20, 20, EmptyMemoryOwner<byte>.EMPTY), 77),
            };

            for (var i = 0; i < messages.Count; i++) { provider.AppendMessage(messages[i]); }

            provider.Dispose();

            Assert.GreaterOrEqual(OutgoingCRDTMessagesProvider.MESSAGES_SHARED_POOL.CountInactive, 1);
            Assert.GreaterOrEqual(OutgoingCRDTMessagesProvider.INDICES_SHARED_POOL.CountInactive, 1);
        }
    }
}
