using CRDT;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.Serialization;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.ECSToCRDTWriter
{
    public class ECSToCRDTWriter : IECSToCRDTWriter
    {
        private readonly ICRDTProtocol crdtProtocol;
        private readonly IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider;
        private readonly Dictionary<int, IComponentSerializer> serializers;

        public ECSToCRDTWriter(ICRDTProtocol crdtProtocol, IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider)
        {
            this.crdtProtocol = crdtProtocol;
            this.outgoingCRDTMessageProvider = outgoingCRDTMessageProvider;
            serializers = new Dictionary<int, IComponentSerializer>();
        }

        public void PutMessage<T>(CRDTEntity crdtID, int componentId, T model)
        {
            if (serializers.TryGetValue(componentId, out IComponentSerializer serializer))
                ProcessMessage(crdtProtocol.CreatePutMessage(crdtID, componentId, serializer.Serialize(model)));
            else
                throw new Exception($"Serializer not present for type {typeof(T).Name}");
        }

        public void AppendMessage<T>(CRDTEntity crdtID, int componentId, T model)
        {
            if (serializers.TryGetValue(componentId, out IComponentSerializer serializer))
                ProcessMessage(crdtProtocol.CreateAppendMessage(crdtID, componentId, serializer.Serialize(model)));
            else
                throw new Exception($"Serializer not present for type {typeof(T).Name}");
        }

        public void DeleteMessage(CRDTEntity crdtID, int componentId)
        {
            ProcessMessage(crdtProtocol.CreateDeleteMessage(crdtID, componentId));
        }

        public void RegisterSerializer(int componentId, IComponentSerializer serializer)
        {
            serializers.Add(componentId, serializer);
        }

        private void ProcessMessage(ProcessedCRDTMessage processedCrdtMessage)
        {
            //We process the message in the CRDTState.
            CRDTReconciliationResult result = crdtProtocol.ProcessMessage(processedCrdtMessage.message);

            //We send the message to the other CRDT if the result was successful in out CRDT
            switch (result.Effect)
            {
                case CRDTReconciliationEffect.ComponentAdded:
                case CRDTReconciliationEffect.ComponentDeleted:
                case CRDTReconciliationEffect.ComponentModified:
                    outgoingCRDTMessageProvider.AddMessage(processedCrdtMessage);
                    break;
                case CRDTReconciliationEffect.NoChanges:
                    break;
                case CRDTReconciliationEffect.EntityDeleted:
                    break;
            }
        }
    }
}
