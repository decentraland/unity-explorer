using CRDT;
using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
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
        private readonly Dictionary<CRDTEntity, string> userIdEntitiesMap = new ();
        private bool reportedSceneReady;

        public SDKObservableEventsEngineAPIImplementation(ISharedPoolsProvider poolsProvider, IInstancePoolsProvider instancePoolsProvider, ICRDTProtocol crdtProtocol, ICRDTDeserializer crdtDeserializer, ICRDTSerializer crdtSerializer,
            ICRDTWorldSynchronizer crdtWorldSynchronizer, IOutgoingCRDTMessagesProvider outgoingCrtdMessagesProvider, ISystemGroupsUpdateGate systemGroupsUpdateGate, ISceneExceptionsHandler exceptionsHandler, MutexSync mutexSync) : base(poolsProvider, instancePoolsProvider, crdtProtocol, crdtDeserializer, crdtSerializer, crdtWorldSynchronizer, outgoingCrtdMessagesProvider, systemGroupsUpdateGate, exceptionsHandler, mutexSync) { }

        public bool EnableSDKObservableMessagesDetection { get; set; } = false;
        public List<SDKObservableEvent> SdkObservableEvents { get; } = new ();
        public HashSet<string> SdkObservableEventSubscriptions { get; } = new ();

        public override void SetIsDisposing()
        {
            userIdEntitiesMap.Clear();
            SdkObservableEvents.Clear();
            SdkObservableEventSubscriptions.Clear();

            base.SetIsDisposing();
        }

        public PoolableSDKObservableEventArray? ConsumeSDKObservableEvents()
        {
            if (SdkObservableEvents.Count == 0) return null;

            PoolableSDKObservableEventArray serializationBufferPoolable = sharedPoolsProvider.GetSerializationSDKObservableEventsPool(SdkObservableEvents.Count);
            for (var i = 0; i < SdkObservableEvents.Count; i++)
            {
                serializationBufferPoolable.Array[i] = SdkObservableEvents[i];
            }
            SdkObservableEvents.Clear();

            return serializationBufferPoolable;
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

            if (!EnableSDKObservableMessagesDetection) return;

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
                    if (EnableSDKObservableMessagesDetection)
                    {
                        bool onEnterSceneSubscribed = SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.EnterScene);
                        bool onPlayerConnectedSubscribed = SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerConnected);

                        if (onEnterSceneSubscribed)
                            SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.EnterScene, new UserIdPayload
                            {
                                userId = playerIdentityData.Address,
                            }));

                        if (onPlayerConnectedSubscribed)
                            SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.PlayerConnected, new UserIdPayload
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
                        if (EnableSDKObservableMessagesDetection)
                        {
                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.LeaveScene))
                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.LeaveScene, new UserIdPayload
                                {
                                    userId = userIdEntitiesMap[pendingMessage.Entity],
                                }));

                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerDisconnected))
                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.PlayerDisconnected, new UserIdPayload
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

                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.SceneReady))
                            {
                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.SceneReady, new SceneReadyPayload()));
                                reportedSceneReady = true;
                            }

                            break;
                        case ComponentID.REALM_INFO: // onRealmChanged observables
                            if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.RealmChanged))
                            {
                                var realmInfo = (PBRealmInfo)message.Message;

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
                                if (!userIdEntitiesMap.ContainsKey(message.Entity)) break;

                                SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.ProfileChanged, new ProfileChangedPayload
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
                        if (SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerExpression))
                        {
                            var avatarEmoteCommand = (PBAvatarEmoteCommand) message.Message;

                            SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.PlayerExpression, new PlayerExpressionPayload
                            {
                                expressionId = avatarEmoteCommand.EmoteUrn,
                            }));
                        }

                    break;
            }
        }
    }
}
