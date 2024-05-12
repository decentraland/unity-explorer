using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.ECS7;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Buffers;
using System.Collections.Generic;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SDKObservableEventsEngineAPIImplementation : EngineAPIImplementation, ISDKObservableEventsEngineApi
    {
        private readonly CRDTPooledMemoryAllocator crdtMemoryAllocator;
        public bool EnableSDKObservableMessagesDetection { get; set; } = false; // TODO: make internal ??
        public List<CRDTMessage> PriorityOutgoingCRDTMessages { get; } = new ();
        public List<CRDTMessage> OutgoingCRDTMessages { get; } = new ();

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider, ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler, MutexSync mutexSync,
            CRDTPooledMemoryAllocator crdtMemoryAllocator) : base(poolsProvider, instancePoolsProvider, crdtProtocol, crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider, systemGroupsUpdateGate, exceptionsHandler, mutexSync)
        {
            this.crdtMemoryAllocator = crdtMemoryAllocator;
        }

        protected override void SyncCRDTMessage(ProcessedCRDTMessage processedMessage)
        {
            if (EnableSDKObservableMessagesDetection && ObservableComponentIDs.Ids.Contains(processedMessage.message.ComponentId))
            {
                CRDTMessage message = processedMessage.message;

                // Copy message to handle its lifecycle separately
                bool isPriorityMessage = message.Type == CRDTMessageType.PUT_COMPONENT && message.ComponentId == ComponentID.PLAYER_IDENTITY_DATA;
                CopyOutgoingMessage(message, isPriorityMessage ? PriorityOutgoingCRDTMessages : OutgoingCRDTMessages);
            }

            base.SyncCRDTMessage(processedMessage);
        }

        private void CopyOutgoingMessage(CRDTMessage targetMessage, List<CRDTMessage> targetCollection)
        {
            IMemoryOwner<byte>? memoryOwnerClone = crdtMemoryAllocator.GetMemoryBuffer(targetMessage.Data.Memory);
            targetMessage.Data.Memory.CopyTo(memoryOwnerClone.Memory);

            targetCollection.Add(new CRDTMessage(
                targetMessage.Type,
                targetMessage.EntityId,
                targetMessage.ComponentId,
                targetMessage.Timestamp,
                memoryOwnerClone));
        }

        public void ClearMessages()
        {
            foreach (CRDTMessage message in PriorityOutgoingCRDTMessages)
                message.Data.Dispose();

            foreach (CRDTMessage message in OutgoingCRDTMessages)
                message.Data.Dispose();

            PriorityOutgoingCRDTMessages.Clear();
            OutgoingCRDTMessages.Clear();
        }
    }
}
