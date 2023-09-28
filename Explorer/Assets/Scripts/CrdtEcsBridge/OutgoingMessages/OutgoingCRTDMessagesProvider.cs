using CRDT.Protocol.Factory;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Pool;
using Utility.ThreadSafePool;

namespace CrdtEcsBridge.OutgoingMessages
{
    public class OutgoingCRTDMessagesProvider : IOutgoingCRTDMessagesProvider
    {
        private class EqualityComparer : IEqualityComparer<OutgoingMessageKey>
        {
            public bool Equals(OutgoingMessageKey x, OutgoingMessageKey y) =>
                x.Entity.Equals(y.Entity) && x.ComponentId == y.ComponentId;

            public int GetHashCode(OutgoingMessageKey obj) =>
                HashCode.Combine(obj.Entity, obj.ComponentId);
        }

        // All OutgoingCRTDMessagesProviders use this pool with a big initial capacity to prevent dynamic allocations
        internal static readonly ThreadSafeDictionaryPool<OutgoingMessageKey, ProcessedCRDTMessage> SHARED_POOL = new (64, PoolConstants.SCENES_COUNT, new EqualityComparer());

        private readonly Dictionary<OutgoingMessageKey, ProcessedCRDTMessage> processedCRDTMessages = SHARED_POOL.Get();
        private readonly Mutex mutex = new ();

        internal IReadOnlyCollection<ProcessedCRDTMessage> ProcessedCRDTMessages => processedCRDTMessages.Values;

        public void AddMessage(ProcessedCRDTMessage processedCRDTMessage)
        {
            mutex.WaitOne();

            // Prevent from writing multiple components representing the same entity and the same component Id
            var key = new OutgoingMessageKey(processedCRDTMessage.message.EntityId, processedCRDTMessage.message.ComponentId);

            if (processedCRDTMessages.TryGetValue(key, out ProcessedCRDTMessage oldMessage))
                oldMessage.message.Data.Dispose();

            // Override with the latest one
            processedCRDTMessages[key] = processedCRDTMessage;
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
