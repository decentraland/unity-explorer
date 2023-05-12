using CRDT.Protocol.Factory;
using System;

namespace CrdtEcsBridge.OutgoingMessages
{
    /// <summary>
    /// Provider of outgoing CRDT messages for the instance of Scene Runtime
    /// </summary>
    public interface IOutgoingCRTDMessagesProvider : IDisposable
    {
        /// <summary>
        /// Add the message to the outgoing CRDT messages.
        /// The call is blocked while the queue is being serialized from the background thread.
        /// Before adding the message you must validate it against CRDT Protocol to ensure that no redundancies are pushed
        /// </summary>
        void AddMessage(ProcessedCRDTMessage processedCRDTMessage);

        /// <summary>
        /// Freeze the modification of the queue while it's being processed from the background thread
        /// </summary>
        /// <returns></returns>
        OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock();
    }
}
