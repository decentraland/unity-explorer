using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.OutgoingMessages;
using Google.Protobuf;
using System;
using System.Buffers;

namespace CrdtEcsBridge.ECSToCRDTWriter
{
    public class ECSToCRDTWriter : IECSToCRDTWriter
    {
        private readonly ICRDTProtocol crdtProtocol;
        private readonly IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider;
        private readonly ISDKComponentsRegistry componentsRegistry;
        private readonly ICRDTMemoryAllocator pooledMemoryAllocator;

        public ECSToCRDTWriter(ICRDTProtocol crdtProtocol, IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider,
            ISDKComponentsRegistry componentsRegistry)
        {
            this.crdtProtocol = crdtProtocol;
            this.outgoingCRDTMessageProvider = outgoingCRDTMessageProvider;
            this.componentsRegistry = componentsRegistry;
            pooledMemoryAllocator = new CRDTPooledMemoryAllocator();
        }

        public void PutMessage<T>(CRDTEntity crdtID, int componentId, T model) where T: IMessage<T>
        {
            if (componentsRegistry.TryGet(componentId, out SDKComponentBridge componentBridge))
            {
                IMemoryOwner<byte> memory = pooledMemoryAllocator.GetMemoryBuffer(model.CalculateSize());
                componentBridge.Serializer.SerializeInto(model, pooledMemoryAllocator.GetMemoryBuffer(model.CalculateSize()).Memory.Span);
                ProcessMessage(crdtProtocol.CreatePutMessage(crdtID, componentId, memory));
            }
            else
                throw new Exception($"Serializer not present for type {typeof(T).Name}");
        }

        public void AppendMessage<T>(CRDTEntity crdtID, int componentId, T model) where T: IMessage<T>
        {
            if (componentsRegistry.TryGet(componentId, out SDKComponentBridge componentBridge))
            {
                IMemoryOwner<byte> memory = pooledMemoryAllocator.GetMemoryBuffer(model.CalculateSize());
                componentBridge.Serializer.SerializeInto(model, pooledMemoryAllocator.GetMemoryBuffer(model.CalculateSize()).Memory.Span);
                ProcessMessage(crdtProtocol.CreateAppendMessage(crdtID, componentId, memory));
            }
            else
                throw new Exception($"Serializer not present for type {typeof(T).Name}");
        }

        public void DeleteMessage(CRDTEntity crdtID, int componentId)
        {
            ProcessMessage(crdtProtocol.CreateDeleteMessage(crdtID, componentId));
        }

        private void ProcessMessage(ProcessedCRDTMessage processedCrdtMessage)
        {
            CRDTReconciliationResult result = crdtProtocol.ProcessMessage(processedCrdtMessage.message);

            switch (result.Effect)
            {
                case CRDTReconciliationEffect.ComponentAdded:
                case CRDTReconciliationEffect.ComponentDeleted:
                case CRDTReconciliationEffect.ComponentModified:
                    outgoingCRDTMessageProvider.AddMessage(processedCrdtMessage);
                    break;
                case CRDTReconciliationEffect.NoChanges:
                    processedCrdtMessage.message.Data.Dispose();
                    break;
                case CRDTReconciliationEffect.EntityDeleted:
                    break;
            }
        }
    }
}
