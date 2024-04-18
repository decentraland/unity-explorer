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
        private readonly List<SDKObservableEvent> sdkObservableEvents = new ();
        private readonly ProtobufSerializer<PBPlayerIdentityData> playerIdentityDataSerializer = new ();
        private readonly HashSet<string> sdkObservableEventSubscriptions = new ();
        private readonly PBPlayerIdentityData playerIdentityData = new ();

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

                    if (sdkObservableEventSubscriptions.Contains(SDKObservableEventIds.EnterScene)
                        && message.Type == CRDTMessageType.PUT_COMPONENT
                        && message.ComponentId == ComponentID.PLAYER_IDENTITY_DATA)
                    {
                        playerIdentityDataSerializer.DeserializeInto(playerIdentityData, message.Data.Memory.Span);

                        sdkObservableEvents.Add(GenerateSDKObservableEvent(SDKObservableEventIds.EnterScene, new UserIdPayload
                        {
                            userId = playerIdentityData.Address,
                        }));
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
