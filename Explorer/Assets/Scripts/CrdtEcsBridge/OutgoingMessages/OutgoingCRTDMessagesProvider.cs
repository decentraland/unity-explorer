using CRDT.Protocol.Factory;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.ThreadSafePool;

namespace CrdtEcsBridge.OutgoingMessages
{
    public class OutgoingCRTDMessagesProvider : IOutgoingCRTDMessagesProvider, IDisposable
    {
        internal const int START_POOL_CAPACITY = 8;

        // All OutgoingCRTDMessagesProviders use this pool with a big initial capacity to prevent dynamic allocations
        internal static readonly ThreadSafeListPool<ProcessedCRDTMessage> SHARED_POOL = new (64, START_POOL_CAPACITY);

        private readonly List<ProcessedCRDTMessage> processedCRDTMessages;

        private readonly Mutex mutex = new ();

        public OutgoingCRTDMessagesProvider()
        {
            processedCRDTMessages = SHARED_POOL.Get();
        }

        internal IReadOnlyList<ProcessedCRDTMessage> ProcessedCRDTMessages => processedCRDTMessages;

        public void AddMessage(ProcessedCRDTMessage processedCRDTMessage)
        {
            mutex.WaitOne();
            processedCRDTMessages.Add(processedCRDTMessage);
            mutex.ReleaseMutex();
        }

        public OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock()
        {
            mutex.WaitOne();

            // Mutex will be released on block.Dispose()
            return new OutgoingCRDTMessagesSyncBlock(processedCRDTMessages, mutex);
        }

        public void Dispose()
        {
            SHARED_POOL.Release(processedCRDTMessages);
            mutex.Dispose();
        }
    }
}
