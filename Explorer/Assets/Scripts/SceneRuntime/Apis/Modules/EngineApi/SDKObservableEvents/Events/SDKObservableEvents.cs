using DCL.ECS7;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events
{
    public static class SDKObservableEventIds
    {
        public static string SceneReady => "sceneStart";
        public static string EnterScene => "onEnterScene";
        public static string LeaveScene => "onLeaveScene";
        public static string PlayerExpression => "playerExpression";
        public static string ProfileChanged => "profileChanged";
        public static string PlayerConnected => "playerConnected";
        public static string PlayerDisconnected => "playerDisconnected";
        public static string RealmChanged => "onRealmChanged";
        public static string PlayerClicked => "playerClicked";
        public static string Comms => "comms";
    }

    public static class SDKObservableComponentIDs
    {
        public static readonly HashSet<int> Ids = new ()
        {
            ComponentID.ENGINE_INFO,
            ComponentID.REALM_INFO,
            ComponentID.PLAYER_IDENTITY_DATA,
            ComponentID.AVATAR_BASE,
            ComponentID.AVATAR_EQUIPPED_DATA,
            ComponentID.AVATAR_EMOTE_COMMAND
        };
    }

    public static class SDKObservableUtils
    {
        [Pure]
        public static SDKObservableEvent NewSDKObservableEventFromData<T>(string eventId, T eventData) where T: struct =>
            new()
            {
                generic = new SDKObservableEvent.Generic
                {
                    eventId = eventId,
                    eventData = JsonConvert.SerializeObject(eventData),
                },
            };
    }

    [PublicAPI]
    public struct SDKObservableEvent
    {
        public Generic generic;

        [PublicAPI]
        public struct Generic
        {
            public string eventId;
            public string eventData; // stringified JSON
        }
    }

    public struct SceneReadyPayload { }

    [PublicAPI]
    public struct CommsPayload
    {
        public string sender;
        public string message;
    }

    [PublicAPI]
    public struct UserIdPayload
    {
        public string userId;
    }

    [PublicAPI]
    public struct PlayerExpressionPayload
    {
        public string expressionId;
    }

    [PublicAPI]
    public struct ProfileChangedPayload
    {
        public string ethAddress;
        public int version;
    }

    [PublicAPI]
    public struct RealmChangedPayload
    {
        public string domain;
        public string room;
        public string serverName;
        public string displayName;
    }

    [PublicAPI]
    public struct PlayerClickedPayload
    {
        public string userId;
        public Ray ray;

        [PublicAPI]
        public struct Ray
        {
            public Vector3 origin;
            public Vector3 direction;
            public float distance;
        }
    }
}
