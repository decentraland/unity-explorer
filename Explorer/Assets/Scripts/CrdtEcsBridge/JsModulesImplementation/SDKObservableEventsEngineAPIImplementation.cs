using CRDT;
using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.Serialization;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.ECS7;
using DCL.ECSComponents;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Collections.Generic;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SDKObservableEventsEngineAPIImplementation : EngineAPIImplementation, ISDKObservableEventsEngineApi
    {
        public bool EnableSDKObservableMessagesDetection { get; set; } = false;
        public List<CRDTMessage> PriorityOutgoingCRDTMessages { get; } = new ();
        public List<CRDTMessage> OutgoingCRDTMessages { get; } = new ();
        public List<SDKObservableEvent> SdkObservableEvents { get; } = new ();
        public HashSet<string> SdkObservableEventSubscriptions { get; } = new ();

        private readonly Dictionary<CRDTEntity, string> userIdEntitiesMap = new ();
        private readonly PBAvatarEmoteCommand avatarEmoteCommand = new ();
        private readonly ProtobufSerializer<PBAvatarEmoteCommand> avatarEmoteCommandSerializer = new ();
        private readonly PBPlayerIdentityData playerIdentityData = new ();
        private readonly ProtobufSerializer<PBPlayerIdentityData> playerIdentityDataSerializer = new ();
        private readonly PBRealmInfo realmInfo = new ();
        private readonly ProtobufSerializer<PBRealmInfo> realmInfoSerializer = new ();
        private bool reportedSceneReady;

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider, ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler, MutexSync mutexSync) : base(poolsProvider, instancePoolsProvider, crdtProtocol, crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider, systemGroupsUpdateGate, exceptionsHandler, mutexSync)
        { }

        protected override void SyncCRDTMessage(ProcessedCRDTMessage processedMessage)
        {
            if (EnableSDKObservableMessagesDetection && SDKObservableComponentIDs.Ids.Contains(processedMessage.message.ComponentId))
                DetectObservableEventsFromComponents(processedMessage.message);

            base.SyncCRDTMessage(processedMessage);
        }

        private void DetectObservableEventsFromComponents(CRDTMessage message)
        {
            switch (message.Type)
                {
                    case CRDTMessageType.PUT_COMPONENT:
                        switch (message.ComponentId)
                        {
                            case ComponentID.PLAYER_IDENTITY_DATA: // onEnterScene + playerConnected observables
                                bool onEnterSceneSubscribed = SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.EnterScene);
                                bool onPlayerConnectedSubscribed = SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerConnected);

                                if (onEnterSceneSubscribed || onPlayerConnectedSubscribed)
                                    playerIdentityDataSerializer.DeserializeInto(playerIdentityData, message.Data.Memory.Span);

                                if (onEnterSceneSubscribed)
                                {
                                    SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.EnterScene, new UserIdPayload
                                    {
                                        userId = playerIdentityData.Address,
                                    }));
                                }

                                if (onPlayerConnectedSubscribed)
                                {
                                    SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.PlayerConnected, new UserIdPayload
                                    {
                                        userId = playerIdentityData.Address,
                                    }));
                                }

                                userIdEntitiesMap[message.EntityId] = playerIdentityData.Address;

                                break;
                            case ComponentID.ENGINE_INFO: // onSceneReady observable
                                if (reportedSceneReady) break;

                                if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.SceneReady))
                                {
                                    SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.SceneReady, new SceneReadyPayload()));
                                    reportedSceneReady = true;
                                }

                                break;
                            case ComponentID.REALM_INFO: // onRealmChanged observables
                                if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.RealmChanged))
                                {
                                    realmInfoSerializer.DeserializeInto(realmInfo, message.Data.Memory.Span);

                                    SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.RealmChanged, new RealmChangedPayload
                                    {
                                        domain = realmInfo.BaseUrl,
                                        room = realmInfo.Room,
                                        displayName = realmInfo.RealmName,
                                        serverName = realmInfo.RealmName,
                                    }));
                                }

                                break;
                            case ComponentID.AVATAR_EQUIPPED_DATA: // profileChanged observable
                            case ComponentID.AVATAR_BASE: // profileChanged observable
                                if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.ProfileChanged))
                                {
                                    if (!userIdEntitiesMap.ContainsKey(message.EntityId)) break;

                                    playerIdentityDataSerializer.DeserializeInto(playerIdentityData, message.Data.Memory.Span);

                                    SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.ProfileChanged, new ProfileChangedPayload
                                    {
                                        ethAddress = userIdEntitiesMap[message.EntityId],
                                        version = 0,
                                    }));
                                }

                                break;
                            /*case ComponentID.POINTER_EVENTS_RESULT: // playerClicked observable
                                break;*/
                        }

                        break;
                    case CRDTMessageType.APPEND_COMPONENT:
                        if (message.ComponentId == ComponentID.AVATAR_EMOTE_COMMAND)
                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerExpression))
                            {
                                avatarEmoteCommandSerializer.DeserializeInto(avatarEmoteCommand, message.Data.Memory.Span);

                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.PlayerExpression, new PlayerExpressionPayload
                                {
                                    expressionId = avatarEmoteCommand.EmoteUrn,
                                }));
                            }

                        break;
                    case CRDTMessageType.DELETE_COMPONENT:
                        // onLeaveScene + playerDisconnected observables
                        if (message.ComponentId == ComponentID.PLAYER_IDENTITY_DATA)
                        {
                            if (!userIdEntitiesMap.ContainsKey(message.EntityId)) break;

                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.LeaveScene))
                            {
                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.LeaveScene, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[message.EntityId],
                                }));
                            }

                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerDisconnected))
                            {
                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.PlayerDisconnected, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[message.EntityId],
                                }));
                            }

                            userIdEntitiesMap.Remove(message.EntityId);
                        }

                        break;
                }
        }

        public List<SDKObservableEvent> ConsumeSDKObservableEvents()
        {
            var eventsCopy = new List<SDKObservableEvent>(SdkObservableEvents);
            SdkObservableEvents.Clear();
            return eventsCopy;
        }

        public override void Dispose()
        {
            SdkObservableEvents.Clear();
            SdkObservableEventSubscriptions.Clear();
            base.Dispose();
        }
    }
}
