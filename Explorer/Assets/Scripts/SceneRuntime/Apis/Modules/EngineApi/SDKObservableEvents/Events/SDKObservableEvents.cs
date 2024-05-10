using DCL.ECS7;
using System.Collections.Generic;
using UnityEngine;

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

    public static class ObservableComponentIDs
    {
        public static readonly List<int> Ids = new List<int>()
        {
            ComponentID.ENGINE_INFO,
            ComponentID.REALM_INFO,
            ComponentID.PLAYER_IDENTITY_DATA,
            ComponentID.AVATAR_BASE,
            ComponentID.AVATAR_EQUIPPED_DATA,
            ComponentID.AVATAR_EMOTE_COMMAND
        };
    }

    public struct SDKObservableEvent
    {
        public Generic generic;

        public struct Generic
        {
            public string eventId;
            public string eventData; // stringified JSON
        }
    }

    public struct SceneReadyPayload { }

    public struct CommsPayload
    {
        public string sender;
        public string message;
    }

    public struct UserIdPayload
    {
        public string userId;
    }

    public struct PlayerExpressionPayload
    {
        public string expressionId;
    }

    public struct ProfileChangedPayload
    {
        public string ethAddress;
        public int version;
    }

    public struct RealmChangedPayload
    {
        public string domain;
        public string room;
        public string serverName;
        public string displayName;
    }

    public struct PlayerClickedPayload
    {
        public string userId;
        public Ray ray;

        public struct Ray
        {
            public Vector3 origin;
            public Vector3 direction;
            public float distance;
        }
    }
}
