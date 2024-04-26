using CRDT;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.Serialization;
using DCL.ECS7;
using DCL.ECSComponents;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public class SDKObservableEventsEngineApiWrapper : EngineApiWrapper
    {
        private readonly PBAvatarEmoteCommand avatarEmoteCommand = new ();
        private readonly ProtobufSerializer<PBAvatarEmoteCommand> avatarEmoteCommandSerializer = new ();
        private readonly PBPlayerIdentityData playerIdentityData = new ();
        private readonly ProtobufSerializer<PBPlayerIdentityData> playerIdentityDataSerializer = new ();
        private readonly List<SDKObservableEvent> sdkObservableEvents = new ();
        private readonly HashSet<string> sdkObservableEventSubscriptions = new ();
        private readonly Dictionary<CRDTEntity, string> userIdEntitiesMap = new ();
        private bool reportedSceneReady;

        public SDKObservableEventsEngineApiWrapper(IEngineApi api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler) : base(api, instancePoolsProvider, exceptionsHandler)
        {
            // TO DEBUG
            /*SubscribeToSDKObservableEvent(SDKObservableEventIds.SceneReady);
            TriggerSDKObservableEvent(SDKObservableEventIds.SceneReady, new SceneStartPayload());*/

            /*SubscribeToSDKObservableEvent(SDKObservableEventIds.EnterScene);
            TriggerSDKObservableEvent(SDKObservableEventIds.EnterScene, new UserIdPayload()
            {
                userId = "0x666-FAKE-USer-ID"
            });*/

            /*SubscribeToSDKObservableEvent(SDKObservableEventIds.LeaveScene);
            TriggerSDKObservableEvent(SDKObservableEventIds.LeaveScene, new UserIdPayload()
            {
                userId = "0x666-FAKE-USer-ID"
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.PlayerConnected);
            TriggerSDKObservableEvent(SDKObservableEventIds.PlayerConnected, new UserIdPayload()
            {
                userId = "0x666-FAKE-USer-ID"
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.PlayerDisconnected);
            TriggerSDKObservableEvent(SDKObservableEventIds.PlayerDisconnected, new UserIdPayload()
            {
                userId = "0x666-FAKE-USer-ID"
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.PlayerExpression);
            TriggerSDKObservableEvent(SDKObservableEventIds.PlayerExpression, new PlayerExpressionPayload()
            {
                expressionId = "Dance"
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.ProfileChanged);
            TriggerSDKObservableEvent(SDKObservableEventIds.ProfileChanged, new ProfileChangedPayload()
            {
                ethAddress = "0x666-FAKE-USer-ID-ADDRESS",
                version = 3
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.PlayerClicked);
            TriggerSDKObservableEvent(SDKObservableEventIds.PlayerClicked, new PlayerClickedPayload()
            {
                userId = "0x666-FAKE-USer-ID",
                ray = new PlayerClickedPayload.Ray()
                {
                    direction = Vector3.forward,
                    distance = 2.68f,
                    origin = new Vector3(0.25f, 0.5f, 1f)
                }
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.VideoEvent);
            TriggerSDKObservableEvent(SDKObservableEventIds.VideoEvent, new VideoEventPayload()
            {
                componentId = "fewfewfewf",
                currentOffset = 0.3f,
                videoStatus = 2,
                totalVideoLength = 50f,
                videoClipId = "fake-video-id"
            });
            SubscribeToSDKObservableEvent(SDKObservableEventIds.RealmChanged);
            TriggerSDKObservableEvent(SDKObservableEventIds.RealmChanged, new RealmChangedPayload()
            {
                domain = "realm-domain",
                room = "realm-room",
                displayName = "realm-display-name",
                serverName = "realm-server-name"
            });*/
        }

        public override void Dispose()
        {
            sdkObservableEvents.Clear();
            sdkObservableEventSubscriptions.Clear();
            base.Dispose();
        }

        [UsedImplicitly]
        public List<SDKObservableEvent> SendBatch()
        {
            try
            {
                sdkObservableEvents.Clear();

                foreach (ProcessedCRDTMessage outgoingCRDTMessage in api.OutgoingCRDTMessages)
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
                                case ComponentID.PLAYER_IDENTITY_DATA: // onEnterScene + playerConnected observables
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

                                // case ComponentID.POINTER_EVENTS_RESULT: // playerClicked observable
                                //     break;
                                // case ComponentID.REALM_INFO: // onRealmChanged observables
                                //     break;
                            }

                            break;
                        case CRDTMessageType.APPEND_COMPONENT:
                            if (message.ComponentId == ComponentID.AVATAR_EMOTE_COMMAND)
                            {
                                if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.PlayerExpression))
                                {
                                    Debug.Log($"PRAVS - DeserializeEmoteCommandSerializer - memoryOwnerID: {message.Data.GetHashCode()}");
                                    avatarEmoteCommandSerializer.DeserializeInto(avatarEmoteCommand, message.Data.Memory.Span);

                                    sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.PlayerExpression, new PlayerExpressionPayload
                                    {
                                        expressionId = avatarEmoteCommand.EmoteUrn,
                                    }));
                                }
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

                api.OutgoingCRDTMessages.Clear();
                return sdkObservableEvents;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return new List<SDKObservableEvent>();
            }
        }

        [UsedImplicitly]
        public void SubscribeToSDKObservableEvent(string eventId)
        {
            sdkObservableEventSubscriptions.Add(eventId);
        }

        [UsedImplicitly]
        public void UnsubscribeFromSDKObservableEvent(string eventId)
        {
            sdkObservableEventSubscriptions.Remove(eventId);
        }

        private SDKObservableEvent GenerateSDKObservableEvent<T>(string eventId, T eventData) where T: struct =>
            new ()
            {
                generic = new SDKObservableEvent.Generic
                {
                    eventId = eventId,
                    eventData = JsonConvert.SerializeObject(eventData), // TODO: Optimize JSON Serialization if needed...
                },
            };
    }
}
