using CRDT;
using Google.Protobuf;
using System;

namespace CrdtEcsBridge.ECSToCRDTWriter
{
    /// <summary>
    ///     Adds a message in the outgoing CRDT messages.
    ///     This message is not immediately serialized but waits for the scene to execute its call.
    ///     Action prepareMessage is needed to avoid manual renting of the message from the pool.
    ///     Action must be static to avoid capturing the instance of the class and parameters
    /// </summary>
    public interface IECSToCRDTWriter
    {
        TMessage PutMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, TData data) where TMessage: class, IMessage;

        /// <summary>
        ///     Put message directly. <br />
        ///     Be careful! If you provide the message manually created the whole scheme will break!
        /// </summary>
        /// <param name="message">Message should be taken from the same component pool</param>
        void PutMessage<TMessage>(TMessage message, CRDTEntity entity) where TMessage: class, IMessage;

        TMessage AppendMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, int timestamp, TData data) where TMessage: class, IMessage;

        void DeleteMessage<T>(CRDTEntity crdtID) where T: class, IMessage;
    }
}
