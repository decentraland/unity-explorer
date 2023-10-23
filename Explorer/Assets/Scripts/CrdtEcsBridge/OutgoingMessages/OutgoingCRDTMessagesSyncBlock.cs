using CRDT.Protocol.Factory;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.OutgoingMessages
{
    /// <summary>
    ///     Prevents modification to the outgoing CRDT messages while they being accessed from the background thread
    /// </summary>
    public readonly struct OutgoingCRDTMessagesSyncBlock : IDisposable
    {
        private readonly List<ProcessedCRDTMessage> messages;

        internal OutgoingCRDTMessagesSyncBlock(List<ProcessedCRDTMessage> messages)
        {
            this.messages = messages;

            PayloadLength = 0;

            for (var i = 0; i < messages.Count; i++)
            {
                ProcessedCRDTMessage processedCRDTMessage = messages[i];
                PayloadLength += processedCRDTMessage.CRDTMessageDataLength;
            }
        }

        public IReadOnlyList<ProcessedCRDTMessage> Messages => messages;

        public int PayloadLength { get; }

        /// <summary>
        ///     Flushes the outgoing CRDT messages and releases the mutex
        /// </summary>
        public void Dispose()
        {
            for (var i = 0; i < messages.Count; i++)
                messages[i].message.Data.Dispose();

            OutgoingCRDTMessagesProvider.MESSAGES_SHARED_POOL.Release(messages);
        }
    }
}
