using CRDT.Deserializer;
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
using System;
using System.Buffers;
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

        protected override void SyncCRDTMessage(ProcessedCRDTMessage message)
        {
            if (message.message.Type == CRDTMessageType.APPEND_COMPONENT
                && message.message.ComponentId == ComponentID.AVATAR_EMOTE_COMMAND)
            {
                // SDKObservableEventsEngineApiWrapper will dispose of the message after reading it
                return;
            }

            base.SyncCRDTMessage(message);
        }
    }
}
