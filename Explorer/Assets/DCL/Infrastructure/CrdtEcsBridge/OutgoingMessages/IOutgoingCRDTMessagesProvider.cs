using CRDT;
using Google.Protobuf;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;

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
        ///     Removes every pending message from the provider in one step and hands them over to the caller; the list
        ///     must be passed to <see cref="GetSerializationSyncBlock(List{OutgoingCRDTMessagesProvider.PendingMessage}, Action{OutgoingCRDTMessagesProvider.PendingMessage})" />,
        ///     which returns it to the pool
        /// </summary>
        List<OutgoingCRDTMessagesProvider.PendingMessage> TakePendingMessages();

        /// <summary>
        ///     Takes the pending messages and serializes them
        /// </summary>
        OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock(Action<OutgoingCRDTMessagesProvider.PendingMessage>? actOnPendingMessage);

        /// <summary>
        ///     Serializes messages previously taken with <see cref="TakePendingMessages" />
        /// </summary>
        OutgoingCRDTMessagesSyncBlock GetSerializationSyncBlock(List<OutgoingCRDTMessagesProvider.PendingMessage> pendingMessages, Action<OutgoingCRDTMessagesProvider.PendingMessage>? actOnPendingMessage);
    }
}
