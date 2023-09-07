using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.OutgoingMessages;
using Diagnostics.ReportsHandling;
using Google.Protobuf;
using System.Buffers;

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

        public void PutMessage<T>(CRDTEntity crdtID, T model) where T: IMessage<T>
        {
            if (!TryGetComponentBridge<T>(out SDKComponentBridge componentBridge)) return;

            IMemoryOwner<byte> memory = memoryAllocator.GetMemoryBuffer(model.CalculateSize());
            componentBridge.Serializer.SerializeInto(model, memory.Memory.Span);
            ProcessMessage(crdtProtocol.CreatePutMessage(crdtID, componentBridge.Id, memory));
        }

        private bool TryGetComponentBridge<T>(out SDKComponentBridge componentBridge) where T: IMessage<T>
        {
            if (!componentsRegistry.TryGet<T>(out componentBridge))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.CRDT_ECS_BRIDGE, ReportHint.AssemblyStatic), $"SDK Component {typeof(T)} is not registered");
                return false;
            }

            return true;
        }

        public void AppendMessage<T>(CRDTEntity crdtID, T model) where T: IMessage<T>
        {
            if (!TryGetComponentBridge<T>(out SDKComponentBridge componentBridge)) return;

            IMemoryOwner<byte> memory = memoryAllocator.GetMemoryBuffer(model.CalculateSize());
            componentBridge.Serializer.SerializeInto(model, memoryAllocator.GetMemoryBuffer(model.CalculateSize()).Memory.Span);
            ProcessMessage(crdtProtocol.CreateAppendMessage(crdtID, componentBridge.Id, memory));
        }

        public void DeleteMessage<T>(CRDTEntity crdtID) where T: IMessage<T>
        {
            if (!TryGetComponentBridge<T>(out SDKComponentBridge componentBridge)) return;

            ProcessMessage(crdtProtocol.CreateDeleteMessage(crdtID, componentBridge.Id));
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
