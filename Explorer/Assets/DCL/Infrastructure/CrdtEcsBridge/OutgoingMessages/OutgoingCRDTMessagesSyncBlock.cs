using CRDT.Protocol.Factory;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.OutgoingMessages
{
    /// <summary>
    ///     Prevents modification to the outgoing CRDT messages while they being accessed from the background thread
    /// </summary>
    public readonly struct OutgoingCRDTMessagesSyncBlock : IDisposable
    {
        internal static readonly ThreadSafeListPool<ProcessedCRDTMessage> MESSAGES_SHARED_POOL =
            new (64, PoolConstants.SCENES_COUNT);

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

        public void Dispose()
        {
            MESSAGES_SHARED_POOL.Release(messages);
        }
    }
}
