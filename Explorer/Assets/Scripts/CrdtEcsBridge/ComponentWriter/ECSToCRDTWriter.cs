using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.OutgoingMessages;
using Google.Protobuf;
using System.Buffers;
using UnityEngine;

namespace CrdtEcsBridge.ComponentWriter
{
    public class ECSToCRDTWriter : IECSToCRDTWriter
    {
        private readonly ICRDTProtocol crdtProtocol;
        private readonly IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider;
        private readonly ISDKComponentsRegistry componentsRegistry;
        private readonly ICRDTMemoryAllocator memoryAllocator;

        public ECSToCRDTWriter(ICRDTProtocol crdtProtocol, IOutgoingCRTDMessagesProvider outgoingCRDTMessageProvider,
            ISDKComponentsRegistry componentsRegistry, ICRDTMemoryAllocator memoryAllocator)
        {
            this.crdtProtocol = crdtProtocol;
            this.outgoingCRDTMessageProvider = outgoingCRDTMessageProvider;
            this.componentsRegistry = componentsRegistry;
            this.memoryAllocator = memoryAllocator;
        }

        public void PutMessage<T>(CRDTEntity crdtID, int componentId, T model) where T: IMessage<T>
        {
            if (!componentsRegistry.TryGet(componentId, out SDKComponentBridge componentBridge))
            {
                Debug.LogWarning($"SDK Component {componentId} is not registered");
                return;
            }

            IMemoryOwner<byte> memory = memoryAllocator.GetMemoryBuffer(model.CalculateSize());
            componentBridge.Serializer.SerializeInto(model, memoryAllocator.GetMemoryBuffer(model.CalculateSize()).Memory.Span);
            ProcessMessage(crdtProtocol.CreatePutMessage(crdtID, componentId, memory));
        }

        public void AppendMessage<T>(CRDTEntity crdtID, int componentId, T model) where T: IMessage<T>
        {
            if (!componentsRegistry.TryGet(componentId, out SDKComponentBridge componentBridge))
            {
                Debug.LogWarning($"SDK Component {componentId} is not registered");
                return;
            }

            IMemoryOwner<byte> memory = memoryAllocator.GetMemoryBuffer(model.CalculateSize());
            componentBridge.Serializer.SerializeInto(model, memoryAllocator.GetMemoryBuffer(model.CalculateSize()).Memory.Span);
            ProcessMessage(crdtProtocol.CreateAppendMessage(crdtID, componentId, memory));
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
