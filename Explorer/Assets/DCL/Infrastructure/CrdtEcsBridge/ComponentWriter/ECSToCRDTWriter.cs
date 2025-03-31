using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.OutgoingMessages;
using Google.Protobuf;
using System;

namespace CrdtEcsBridge.ComponentWriter
{
    public class ECSToCRDTWriter : IECSToCRDTWriter
    {
        private readonly IOutgoingCRDTMessagesProvider outgoingCRDTMessageProvider;

        public ECSToCRDTWriter(IOutgoingCRDTMessagesProvider outgoingCRDTMessageProvider)
        {
            this.outgoingCRDTMessageProvider = outgoingCRDTMessageProvider;
        }

        public TMessage PutMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, TData data) where TMessage: class, IMessage =>
            outgoingCRDTMessageProvider.AddPutMessage(prepareMessage, entity, data);

        public void PutMessage<TMessage>(TMessage message, CRDTEntity entity) where TMessage: class, IMessage
        {
            outgoingCRDTMessageProvider.AddPutMessage(message, entity);
        }

        public TMessage AppendMessage<TMessage, TData>(Action<TMessage, TData> prepareMessage, CRDTEntity entity, int timestamp, TData data) where TMessage: class, IMessage =>
            outgoingCRDTMessageProvider.AppendMessage(prepareMessage, entity, timestamp, data);

        public void DeleteMessage<T>(CRDTEntity crdtID) where T: class, IMessage
        {
            outgoingCRDTMessageProvider.AddDeleteMessage<T>(crdtID);
        }
    }
}
