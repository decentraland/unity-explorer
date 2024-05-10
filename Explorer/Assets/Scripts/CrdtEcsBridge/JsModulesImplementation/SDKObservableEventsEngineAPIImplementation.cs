using CRDT.Deserializer;
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
using System.Collections.Generic;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SDKObservableEventsEngineAPIImplementation : EngineAPIImplementation, ISDKObservableEventsEngineApi
    {
        public bool EnableSDKObservableMessagesDetection { get; set; } = false; // TODO: make internal ??
        public List<ProcessedCRDTMessage> OutgoingCRDTMessages { get; } = new ();

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer, ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider, ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler, MutexSync mutexSync) : base(poolsProvider, instancePoolsProvider, crdtProtocol, crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider, systemGroupsUpdateGate, exceptionsHandler, mutexSync)
        {
        }

        protected override void SyncCRDTMessage(ProcessedCRDTMessage message)
        {
            if (EnableSDKObservableMessagesDetection && ObservableComponentIDs.Ids.Contains(message.message.ComponentId))
            {
                // Copy message to handle its lifecycle separately
                OutgoingCRDTMessages.Add(new ProcessedCRDTMessage(message.message, message.CRDTMessageDataLength));
            }

            base.SyncCRDTMessage(message);
        }

        public void ClearMessages()
        {
            foreach (ProcessedCRDTMessage message in OutgoingCRDTMessages)
            {
                message.message.Data.Dispose();
            }

            OutgoingCRDTMessages.Clear();
        }
    }
}
