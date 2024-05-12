using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SDKObservableEventsEngineAPIImplementation : EngineAPIImplementation, ISDKObservableEventsEngineApi
    {
        public bool EnableSDKObservableMessagesDetection { get; set; } = false; // TODO: make internal ??
        public List<CRDTMessage> OutgoingCRDTMessages { get; } = new ();

        private readonly CRDTPooledMemoryAllocator crdtMemoryAllocator;

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer, ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider, ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler, MutexSync mutexSync, CRDTPooledMemoryAllocator crdtMemoryAllocator) : base(poolsProvider, instancePoolsProvider, crdtProtocol, crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider, systemGroupsUpdateGate, exceptionsHandler, mutexSync)
        {
            this.crdtMemoryAllocator = crdtMemoryAllocator;
        }

        protected override void SyncCRDTMessage(ProcessedCRDTMessage message)
        {
            if (EnableSDKObservableMessagesDetection && ObservableComponentIDs.Ids.Contains(message.message.ComponentId))
            {
                // Copy message to handle its lifecycle separately
                var memoryOwnerClone = crdtMemoryAllocator.GetMemoryBuffer(message.message.Data.Memory.Length);
                message.message.Data.Memory.CopyTo(memoryOwnerClone.Memory);

                OutgoingCRDTMessages.Add(new CRDTMessage(
                    message.message.Type,
                    message.message.EntityId,
                    message.message.ComponentId,
                    message.message.Timestamp,
                    memoryOwnerClone));
            }

            base.SyncCRDTMessage(message);
        }

        public void ClearMessages()
        {
            foreach (CRDTMessage message in OutgoingCRDTMessages)
            {
                message.Data.Dispose();
            }

            OutgoingCRDTMessages.Clear();
        }
    }
}
