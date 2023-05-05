using CRDT.Protocol;
using CRDT.Protocol.Factory;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace CrdtEcsBridge.OutgoingMessages.Tests
{
    public class OutgoingCRTDMessagesProviderShould
    {
        private OutgoingCRTDMessagesProvider provider;

        [SetUp]
        public void SetUp()
        {
            provider = new OutgoingCRTDMessagesProvider();
        }

        [Test]
        public void AddMessage()
        {
            var messages = new List<ProcessedCRDTMessage>
            {
                new (new (CRDTMessageType.PUT_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 50),
                new (new (CRDTMessageType.DELETE_ENTITY, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 12),
                new (new (CRDTMessageType.APPEND_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 34),
                new (new (CRDTMessageType.DELETE_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 77),
            };

            for (var i = 0; i < messages.Count; i++) { provider.AddMessage(messages[i]); }

            CollectionAssert.AreEqual(messages, provider.ProcessedCRDTMessages);
        }

        [Test]
        public void AddWaitForMutexRelease()
        {
            var messages = new List<ProcessedCRDTMessage>
            {
                new (new (CRDTMessageType.PUT_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 50),
                new (new (CRDTMessageType.DELETE_ENTITY, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 12),
                new (new (CRDTMessageType.APPEND_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 34),
                new (new (CRDTMessageType.DELETE_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 77),
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

            for (var i = 0; i < messages.Count; i++) { provider.AddMessage(messages[i]); }

            watch.Stop();

            Assert.GreaterOrEqual(watch.Elapsed.TotalSeconds, 1);
        }

        [Test]
        public void ReleaseToPoolOnDispose()
        {
            var messages = new List<ProcessedCRDTMessage>
            {
                new (new (CRDTMessageType.PUT_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 50),
                new (new (CRDTMessageType.DELETE_ENTITY, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 12),
                new (new (CRDTMessageType.APPEND_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 34),
                new (new (CRDTMessageType.DELETE_COMPONENT, 10, 20, 20, ReadOnlyMemory<byte>.Empty), 77),
            };

            for (var i = 0; i < messages.Count; i++) { provider.AddMessage(messages[i]); }

            provider.Dispose();

            Assert.AreEqual(1, OutgoingCRTDMessagesProvider.SHARED_POOL.CountInactive);
        }
    }
}
