using CRDT.Protocol.Factory;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CrdtEcsBridge.OutgoingMessages
{
    /// <summary>
    /// Prevents modification to the outgoing CRDT messages while they being accessed from the background thread
    /// </summary>
    public readonly struct OutgoingCRDTMessagesSyncBlock : IDisposable
    {
        private readonly Mutex mutex;
        private readonly Dictionary<OutgoingMessageKey, ProcessedCRDTMessage> messages;

        internal OutgoingCRDTMessagesSyncBlock(Dictionary<OutgoingMessageKey, ProcessedCRDTMessage> messages, Mutex mutex)
        {
            this.messages = messages;
            this.mutex = mutex;
        }

        public IReadOnlyCollection<ProcessedCRDTMessage> Messages => messages.Values;

        /// <summary>
        /// Returns the total size of the payload of the outgoing CRDT Messages
        /// </summary>
        public int GetPayloadLength()
        {
            var length = 0;

            foreach (ProcessedCRDTMessage processedCRDTMessage in messages.Values)
                length += processedCRDTMessage.CRDTMessageDataLength;

            return length;
        }

        /// <summary>
        /// Flushes the outgoing CRDT messages and releases the mutex
        /// </summary>
        public void Dispose()
        {
            messages.Clear();
            mutex.ReleaseMutex();
        }
    }
}
