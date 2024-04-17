using UnityEngine;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public static class SDKObservableEventIds
    {
        public static string SceneReady => "sceneStart";
        public static string EnterScene => "onEnterScene";
        public static string LeaveScene => "onLeaveScene";
        public static string PlayerExpression => "playerExpression";
        public static string VideoEvent => "videoEvent";
        public static string ProfileChanged => "profileChanged";
        public static string PlayerConnected => "playerConnected";
        public static string PlayerDisconnected => "playerDisconnected";
        public static string RealmChanged => "onRealmChanged";
        public static string PlayerClicked => "playerClicked";
        public static string Comms => "comms";
    }

    // TODO: make clear in COMMENTS these observables can be removed/flagged to only use observable-replacement components
    public struct SDKObservableEvent {
        public struct Generic
        {
            public string eventId;
            public string eventData; // stringified JSON
        }

        public Generic generic;
    }

    public struct SceneStartPayload { }

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

    public struct VideoEventPayload {
        public string componentId;
        public string videoClipId;
        /** Status, can be NONE = 0, ERROR = 1, LOADING = 2, READY = 3, PLAYING = 4,BUFFERING = 5 */
        public int videoStatus;

        /** Current offset position in seconds */
        public float currentOffset;
        /** Video length in seconds. Can be -1 */
        public float totalVideoLength;
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
        public struct Ray
        {
            public Vector3 origin;
            public Vector3 direction;
            public float distance;
        }

        public string userId;
        public Ray ray;
    }
}
