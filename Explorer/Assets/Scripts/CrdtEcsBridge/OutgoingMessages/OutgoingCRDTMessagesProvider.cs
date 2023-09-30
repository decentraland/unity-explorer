using CRDT.Protocol.Factory;
using System;
using System.Collections.Generic;
using Utility.Pool;
using Utility.ThreadSafePool;

namespace CrdtEcsBridge.OutgoingMessages
{
    public class OutgoingCRDTMessagesProvider : IOutgoingCRDTMessagesProvider
    {
        private class EqualityComparer : IEqualityComparer<OutgoingMessageKey>
        {
            public bool Equals(OutgoingMessageKey x, OutgoingMessageKey y) =>
                x.Entity.Equals(y.Entity) && x.ComponentId == y.ComponentId;

            public int GetHashCode(OutgoingMessageKey obj) =>
                HashCode.Combine(obj.Entity, obj.ComponentId);
        }

        internal static readonly ThreadSafeDictionaryPool<OutgoingMessageKey, int> INDICES_SHARED_POOL =
            new (64, PoolConstants.SCENES_COUNT, new EqualityComparer());

        internal static readonly ThreadSafeListPool<ProcessedCRDTMessage> MESSAGES_SHARED_POOL =
            new (64, PoolConstants.SCENES_COUNT);

        internal readonly Dictionary<OutgoingMessageKey, int> lwwMessageIndices = INDICES_SHARED_POOL.Get();
        internal readonly List<ProcessedCRDTMessage> messages = MESSAGES_SHARED_POOL.Get();

        public void AddLwwMessage(ProcessedCRDTMessage processedCRDTMessage)
        {
            lock (messages)
            {
                var key = new OutgoingMessageKey(processedCRDTMessage.message.EntityId, processedCRDTMessage.message.ComponentId);

                if (lwwMessageIndices.TryGetValue(key, out int lwwIndex))
                    messages[lwwIndex] = processedCRDTMessage;
                else
                {
                    lwwMessageIndices[key] = messages.Count;
                    messages.Add(processedCRDTMessage);
                }
            }
        }

        public void AppendMessage(ProcessedCRDTMessage processedCRDTMessage)
        {
            lock (messages) { messages.Add(processedCRDTMessage); }
        }

        public OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock()
        {
            // Make a copy of the list and process it
            // While we do it we must synchronize

            List<ProcessedCRDTMessage> listCopy = MESSAGES_SHARED_POOL.Get();

            lock (messages) { listCopy.AddRange(messages); }

            // A copy will be released on block.Dispose()

            return new OutgoingCRDTMessagesSyncBlock(listCopy);
        }

        public void Dispose()
        {
            INDICES_SHARED_POOL.Release(lwwMessageIndices);
            MESSAGES_SHARED_POOL.Release(messages);
        }
    }
}
