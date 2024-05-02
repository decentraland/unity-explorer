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
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SDKObservableEventsEngineAPIImplementation : EngineAPIImplementation, ISDKObservableEventsEngineApi
    {
        public HashSet<ProcessedCRDTMessage> OutgoingCRDTMessages { get; } = new ();

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer, ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider, ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler, MutexSync mutexSync) : base(poolsProvider, instancePoolsProvider, crdtProtocol, crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider, systemGroupsUpdateGate, exceptionsHandler, mutexSync)
        {
        }

        protected override void SerializeProcessedMessage(ref Span<byte> span, in ProcessedCRDTMessage processedCRDTMessage)
        {
            OutgoingCRDTMessages.Add(processedCRDTMessage);
            base.SerializeProcessedMessage(ref span, in processedCRDTMessage);
        }
    }
}
