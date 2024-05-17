using CRDT;
using CRDT.Protocol;
using Google.Protobuf;
using JetBrains.Annotations;
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
        [CanBeNull] TMessage AddPutMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, TData data) where TMessage: class, IMessage;

        void AddPutMessage<TMessage>(TMessage message, CRDTEntity entity) where TMessage: class, IMessage;

        /// <summary>
        ///     Append the message without overriding
        /// </summary>
        [CanBeNull] TMessage AppendMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, int timestamp, TData data) where TMessage: class, IMessage;

        void AddDeleteMessage<TMessage>(CRDTEntity entity) where TMessage: class, IMessage;

        /// <summary>
        ///     Freeze the modification of the queue while it's being processed from the background thread
        /// </summary>
        /// <returns></returns>
        OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock();
    }
}
