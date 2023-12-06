using CRDT;
using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.OutgoingMessages;
using DCL.Diagnostics;
using Google.Protobuf;
using System.Buffers;

namespace CrdtEcsBridge.ComponentWriter
{
    public class ECSToCRDTWriter : IECSToCRDTWriter
    {
        private readonly ICRDTProtocol crdtProtocol;
        private readonly IOutgoingCRDTMessagesProvider outgoingCRDTMessageProvider;
        private readonly ISDKComponentsRegistry componentsRegistry;
        private readonly ICRDTMemoryAllocator memoryAllocator;

        public ECSToCRDTWriter(ICRDTProtocol crdtProtocol, IOutgoingCRDTMessagesProvider outgoingCRDTMessageProvider,
            ISDKComponentsRegistry componentsRegistry, ICRDTMemoryAllocator memoryAllocator)
        {
            this.crdtProtocol = crdtProtocol;
            this.outgoingCRDTMessageProvider = outgoingCRDTMessageProvider;
            this.componentsRegistry = componentsRegistry;
            this.memoryAllocator = memoryAllocator;
        }

        public void PutMessage<T>(CRDTEntity crdtID, T model) where T: IMessage
        {
            if (!TryGetComponentBridge<T>(out SDKComponentBridge componentBridge)) return;

            IMemoryOwner<byte> memory = memoryAllocator.GetMemoryBuffer(model.CalculateSize());
            componentBridge.Serializer.SerializeInto(model, memory.Memory.Span);
            outgoingCRDTMessageProvider.AddLwwMessage(crdtProtocol.CreatePutMessage(crdtID, componentBridge.Id, memory));
        }

        private bool TryGetComponentBridge<T>(out SDKComponentBridge componentBridge) where T: IMessage
        {
            if (!componentsRegistry.TryGet<T>(out componentBridge))
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.CRDT_ECS_BRIDGE, ReportHint.AssemblyStatic), $"SDK Component {typeof(T)} is not registered");
                return false;
            }

            return true;
        }

        public void AppendMessage<T>(CRDTEntity crdtID, T model, int timestamp = 0) where T: IMessage
        {
            if (!TryGetComponentBridge<T>(out SDKComponentBridge componentBridge)) return;

            IMemoryOwner<byte> memory = memoryAllocator.GetMemoryBuffer(model.CalculateSize());
            componentBridge.Serializer.SerializeInto(model, memory.Memory.Span);
            outgoingCRDTMessageProvider.AppendMessage(crdtProtocol.CreateAppendMessage(crdtID, componentBridge.Id, timestamp, memory));
        }

        public void DeleteMessage<T>(CRDTEntity crdtID) where T: IMessage
        {
            if (!TryGetComponentBridge<T>(out SDKComponentBridge componentBridge)) return;

            outgoingCRDTMessageProvider.AddLwwMessage(crdtProtocol.CreateDeleteMessage(crdtID, componentBridge.Id));
        }
    }
}
