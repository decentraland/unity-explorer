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
        private readonly List<ProcessedCRDTMessage> messages;

        internal OutgoingCRDTMessagesSyncBlock(List<ProcessedCRDTMessage> messages, Mutex mutex)
        {
            this.messages = messages;
            this.mutex = mutex;
        }

        public IReadOnlyList<ProcessedCRDTMessage> Messages => messages;

        /// <summary>
        /// Returns the total size of the payload of the outgoing CRDT Messages
        /// </summary>
        public int GetPayloadLength()
        {
            var length = 0;

            for (var i = 0; i < messages.Count; i++)
                length += messages[i].CRDTMessageDataLength;

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
