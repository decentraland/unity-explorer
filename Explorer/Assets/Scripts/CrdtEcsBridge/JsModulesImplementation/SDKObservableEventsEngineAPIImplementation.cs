using CRDT;
using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.Diagnostics;
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
        private readonly Dictionary<CRDTEntity, string> userIdEntitiesMap = new ();
        private readonly List<SDKObservableEvent> sdkObservableEvents = new ();
        private readonly HashSet<string> sdkObservableEventSubscriptions = new ();
        private bool enableSDKObservableMessagesDetection;
        private bool reportedSceneReady;

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider,
            ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler,
            MultithreadSync multithreadSync) : base(poolsProvider, instancePoolsProvider, crdtProtocol,
            crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider,
            systemGroupsUpdateGate, exceptionsHandler, multithreadSync) { }

        public void TryAddSubscription(string eventId)
        {
            if (eventId == SDKObservableEventIds.PlayerClicked)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.SDK_OBSERVABLES), "Scene subscribed to unsupported SDK Observable 'PlayerClicked'");
                return;
            }

            sdkObservableEventSubscriptions.Add(eventId);
            enableSDKObservableMessagesDetection = true;
        }

        public void RemoveSubscriptionIfExists(string eventId)
        {
            sdkObservableEventSubscriptions.Remove(eventId);

            if (sdkObservableEventSubscriptions.Count == 0)
                enableSDKObservableMessagesDetection = false;
        }

        public bool HasSubscription(string eventId) =>
            sdkObservableEventSubscriptions.Contains(eventId);

        public bool IsAnySubscription() =>
            sdkObservableEventSubscriptions.Count > 0;

        public void AddSDKObservableEvent(SDKObservableEvent sdkObservableEvent)
        {
            sdkObservableEvents.Add(sdkObservableEvent);
        }

        public void ClearSDKObservableEvents()
        {
            sdkObservableEvents.Clear();
        }

        public PoolableSDKObservableEventArray? ConsumeSDKObservableEvents()
        {
            if (sdkObservableEvents.Count == 0) return null;

            PoolableSDKObservableEventArray serializationBufferPoolable = sharedPoolsProvider.GetSerializationSDKObservableEventsPool(sdkObservableEvents.Count);

            for (var i = 0; i < sdkObservableEvents.Count; i++) { serializationBufferPoolable.Array[i] = sdkObservableEvents[i]; }

            sdkObservableEvents.Clear();

            return serializationBufferPoolable;
        }

        public override void SetIsDisposing()
        {
            userIdEntitiesMap.Clear();
            sdkObservableEvents.Clear();
            sdkObservableEventSubscriptions.Clear();

            base.SetIsDisposing();
        }

        protected override void ProcessPendingMessage(OutgoingCRDTMessagesProvider.PendingMessage pendingMessage)
        {
            if (SDKObservableComponentIDs.Ids.Contains(pendingMessage.Bridge.Id))
                DetectObservableEventsFromComponents(pendingMessage);
        }

        private void DetectObservableEventsFromComponents(OutgoingCRDTMessagesProvider.PendingMessage pendingMessage)
        {
            // We must always detect PlayerIdentityData messages to have the entities map updated
            // for scenes that may subscribe to observables later in their execution
            DetectPlayerIdentityDataComponent(pendingMessage);

            if (!enableSDKObservableMessagesDetection) return;

            DetectOtherObservableComponents(pendingMessage);
        }

        private void DetectPlayerIdentityDataComponent(OutgoingCRDTMessagesProvider.PendingMessage pendingMessage)
        {
            if (pendingMessage.Bridge.Id != ComponentID.PLAYER_IDENTITY_DATA) return;

            var playerIdentityData = (PBPlayerIdentityData)pendingMessage.Message;

            switch (pendingMessage.MessageType)
            {
                case CRDTMessageType.PUT_COMPONENT:
                    // onEnterScene + playerConnected observables
                    if (enableSDKObservableMessagesDetection)
                    {
                        bool onEnterSceneSubscribed = sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.EnterScene);
                        bool onPlayerConnectedSubscribed = sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerConnected);

                        if (onEnterSceneSubscribed)
                            sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.EnterScene, new UserIdPayload
                            {
                                userId = playerIdentityData.Address,
                            }));

                        if (onPlayerConnectedSubscribed)
                            sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.PlayerConnected, new UserIdPayload
                            {
                                userId = playerIdentityData.Address,
                            }));
                    }

                    userIdEntitiesMap[pendingMessage.Entity] = playerIdentityData.Address;
                    break;
                case CRDTMessageType.DELETE_COMPONENT:
                    if (userIdEntitiesMap.ContainsKey(pendingMessage.Entity)) // we may get more than 1 DELETE_COMPONENT of the same component
                    {
                        // onLeaveScene + playerDisconnected observables
                        if (enableSDKObservableMessagesDetection)
                        {
                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.LeaveScene))
                                sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.LeaveScene, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[pendingMessage.Entity],
                                }));

                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerDisconnected))
                                sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.PlayerDisconnected, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[pendingMessage.Entity],
                                }));
                        }

                        userIdEntitiesMap.Remove(pendingMessage.Entity);
                    }

                    break;
            }
        }

        private void DetectOtherObservableComponents(OutgoingCRDTMessagesProvider.PendingMessage message)
        {
            switch (message.MessageType)
            {
                case CRDTMessageType.PUT_COMPONENT:
                    switch (message.Bridge.Id)
                    {
                        case ComponentID.ENGINE_INFO: // onSceneReady observable
                            if (reportedSceneReady) break;

                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.SceneReady))
                            {
                                sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.SceneReady, new SceneReadyPayload()));
                                reportedSceneReady = true;
                            }

                            break;
                        case ComponentID.REALM_INFO: // onRealmChanged observables
                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.RealmChanged))
                            {
                                var realmInfo = (PBRealmInfo)message.Message;

                                sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.RealmChanged, new RealmChangedPayload
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
                            if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.ProfileChanged))
                            {
                                if (!userIdEntitiesMap.ContainsKey(message.Entity)) break;

                                sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.ProfileChanged, new ProfileChangedPayload
                                {
                                    ethAddress = userIdEntitiesMap[message.Entity],
                                    version = 0,
                                }));
                            }

                            break;
                        /*case ComponentID.POINTER_EVENTS_RESULT: // playerClicked observable
                            break;*/
                    }

                    break;
                case CRDTMessageType.APPEND_COMPONENT:
                    if (message.Bridge.Id == ComponentID.AVATAR_EMOTE_COMMAND)
                        if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerExpression))
                        {
                            var avatarEmoteCommand = (PBAvatarEmoteCommand)message.Message;

                            sdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.PlayerExpression, new PlayerExpressionPayload
                            {
                                expressionId = avatarEmoteCommand.EmoteUrn,
                            }));
                        }

                    break;
            }
        }
    }
}
