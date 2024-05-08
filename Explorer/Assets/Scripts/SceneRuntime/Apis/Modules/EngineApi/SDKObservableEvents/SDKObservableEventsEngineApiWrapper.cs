using CRDT;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.Serialization;
using DCL.Diagnostics;
using DCL.ECS7;
using DCL.ECSComponents;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public class SDKObservableEventsEngineApiWrapper : EngineApiWrapper
    {
        private static List<SDKObservableEvent> EMPTY_EVENTS_LIST = new ();

        private readonly PBAvatarEmoteCommand avatarEmoteCommand = new ();
        private readonly ProtobufSerializer<PBAvatarEmoteCommand> avatarEmoteCommandSerializer = new ();
        private readonly PBPlayerIdentityData playerIdentityData = new ();
        private readonly ProtobufSerializer<PBPlayerIdentityData> playerIdentityDataSerializer = new ();
        private readonly PBRealmInfo realmInfo = new ();
        private readonly ProtobufSerializer<PBRealmInfo> realmInfoSerializer = new ();
        private readonly List<SDKObservableEvent> sdkObservableEvents = new ();
        private readonly HashSet<string> sdkObservableEventSubscriptions = new ();
        private readonly Dictionary<CRDTEntity, string> userIdEntitiesMap = new ();
        private readonly ISDKObservableEventsEngineApi engineApi;
        private readonly ISDKMessageBusCommsControllerAPI commsApi;
        private bool reportedSceneReady;

        public SDKObservableEventsEngineApiWrapper(ISDKObservableEventsEngineApi api, ISDKMessageBusCommsControllerAPI commsApi, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler) : base(api, instancePoolsProvider, exceptionsHandler)
        {
            this.engineApi = api;
            this.commsApi = commsApi;
        }

        public override void Dispose()
        {
            sdkObservableEvents.Clear();
            sdkObservableEventSubscriptions.Clear();
            base.Dispose();
        }

        // Used for SDK Observables + SDK Comms MessageBus
        [UsedImplicitly]
        public List<SDKObservableEvent> SendBatch()
        {
            if (sdkObservableEventSubscriptions.Count == 0)
            {
                engineApi.ClearOutgoingCRDTMessages();
                return EMPTY_EVENTS_LIST;
            }

            try
            {
                sdkObservableEvents.Clear();

                DetectObservableEventsFromComponents();

                // SDK 'comms' observable for scenes MessageBus
                DetectSceneMessageBusCommsObservableEvent();

                return sdkObservableEvents;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return EMPTY_EVENTS_LIST;
            }
        }

        private void DetectObservableEventsFromComponents()
        {
            // 1. Due to uncertainty in CRDT messages order we have to first check those which populate the userIdEntitiesMap
            foreach (ProcessedCRDTMessage outgoingCRDTMessage in engineApi.OutgoingCRDTMessages)
            {
                CRDTMessage message = outgoingCRDTMessage.message;

                if (message.ComponentId != ComponentID.PLAYER_IDENTITY_DATA || message.Type != CRDTMessageType.PUT_COMPONENT)
                    continue;

                // onEnterScene + playerConnected observables
                bool onEnterSceneSubscribed = sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.EnterScene);
                bool onPlayerConnectedSubscribed = sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerConnected);

                if (onEnterSceneSubscribed || onPlayerConnectedSubscribed)
                    playerIdentityDataSerializer.DeserializeInto(playerIdentityData, message.Data.Memory.Span);

                if (onEnterSceneSubscribed)
                {
                    sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.EnterScene, new UserIdPayload
                    {
                        userId = playerIdentityData.Address,
                    }));
                }

                if (onPlayerConnectedSubscribed)
                {
                    sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.PlayerConnected, new UserIdPayload
                    {
                        userId = playerIdentityData.Address,
                    }));
                }

                userIdEntitiesMap[message.EntityId] = playerIdentityData.Address;
            }

            // 2. then all the other messages are checked
            foreach (ProcessedCRDTMessage outgoingCRDTMessage in engineApi.OutgoingCRDTMessages)
            {
                CRDTMessage message = outgoingCRDTMessage.message;

                switch (message.Type)
                {
                    case CRDTMessageType.PUT_COMPONENT:
                        switch (message.ComponentId)
                        {
                            case ComponentID.ENGINE_INFO: // onSceneReady observable
                                if (reportedSceneReady) continue;

                                if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.SceneReady))
                                {
                                    sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.SceneReady, new SceneReadyPayload()));
                                    reportedSceneReady = true;
                                }

                                break;
                            case ComponentID.REALM_INFO: // onRealmChanged observables
                                if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.RealmChanged))
                                {
                                    realmInfoSerializer.DeserializeInto(realmInfo, message.Data.Memory.Span);
                                    sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.RealmChanged, new RealmChangedPayload()
                                    {
                                        domain = realmInfo.BaseUrl,
                                        room = realmInfo.Room,
                                        displayName = realmInfo.RealmName,
                                        serverName = realmInfo.RealmName
                                    }));
                                }
                                break;
                            case ComponentID.AVATAR_EQUIPPED_DATA: // profileChanged observable
                            case ComponentID.AVATAR_BASE: // profileChanged observable
                                if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.ProfileChanged))
                                {
                                    playerIdentityDataSerializer.DeserializeInto(playerIdentityData, message.Data.Memory.Span);

                                    sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.ProfileChanged, new ProfileChangedPayload
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
                        {
                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerExpression))
                            {
                                avatarEmoteCommandSerializer.DeserializeInto(avatarEmoteCommand, message.Data.Memory.Span);

                                sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.PlayerExpression, new PlayerExpressionPayload
                                {
                                    expressionId = avatarEmoteCommand.EmoteUrn,
                                }));
                            }

                            // Release message memory as it's not needed anymore
                            message.Data.Dispose();
                        }

                        break;
                    case CRDTMessageType.DELETE_COMPONENT:
                        // onLeaveScene + playerDisconnected observables
                        if (message.ComponentId == ComponentID.PLAYER_IDENTITY_DATA)
                        {
                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.LeaveScene))
                            {
                                sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.LeaveScene, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[message.EntityId],
                                }));
                            }

                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerDisconnected))
                            {
                                sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.PlayerDisconnected, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[message.EntityId],
                                }));
                            }

                            userIdEntitiesMap.Remove(message.EntityId);
                        }

                        break;
                }
            }

            engineApi.ClearOutgoingCRDTMessages();
        }

        private void DetectSceneMessageBusCommsObservableEvent()
        {
            if (!sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.Comms))
                return;

            foreach (CommsPayload currentPayload in commsApi.SceneCommsMessages)
            {
                sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.Comms, currentPayload));
            }
            commsApi.SceneCommsMessages.Clear();
        }

        [UsedImplicitly]
        public void SubscribeToSDKObservableEvent(string eventId)
        {
            sdkObservableEventSubscriptions.Add(eventId);

            if (eventId == SDKObservableEventIds.PlayerClicked)
                ReportHub.LogWarning(new ReportData(ReportCategory.SDK_OBSERVABLES), "Scene subscribed to unsupported SDK Observable 'PlayerClicked'");
        }

        [UsedImplicitly]
        public void UnsubscribeFromSDKObservableEvent(string eventId)
        {
            sdkObservableEventSubscriptions.Remove(eventId);
        }

        private SDKObservableEvent GenerateSDKObservableEvent<T>(string eventId, T eventData) where T: struct
        {
            return new ()
            {
                generic = new SDKObservableEvent.Generic
                {
                    eventId = eventId,
                    eventData = JsonConvert.SerializeObject(eventData)
                },
            };
        }
    }
}
