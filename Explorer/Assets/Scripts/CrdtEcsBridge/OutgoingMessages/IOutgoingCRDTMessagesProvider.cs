using CRDT.Protocol.Factory;
using System;

namespace CrdtEcsBridge.OutgoingMessages
{
    /// <summary>
    ///     Provider of outgoing CRDT messages for the instance of Scene Runtime
    /// </summary>
    public interface IOutgoingCRDTMessagesProvider : IDisposable
    {
        /// <summary>
        ///     Add the message to the outgoing CRDT messages.
        ///     Override the message if the same combination of entity and component Id already exists
        /// </summary>
        void AddLwwMessage(ProcessedCRDTMessage processedCRDTMessage);

        /// <summary>
        ///     Append the message without overriding
        /// </summary>
        void AppendMessage(ProcessedCRDTMessage processedCRDTMessage);

        /// <summary>
        ///     Freeze the modification of the queue while it's being processed from the background thread
        /// </summary>
        /// <returns></returns>
        OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock();
    }
}
