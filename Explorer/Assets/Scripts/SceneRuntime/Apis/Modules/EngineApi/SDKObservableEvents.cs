using UnityEngine;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public struct SDKObserbableEventPayload {}

    public struct SceneStart { }

    public struct Comms
    {
        public string sender;
        public string message;
    }

    public struct OnEnterScene
    {
        public string userId;
    }

    public struct OnLeaveScene
    {
        public string userId;
    }

    public struct PlayerExpression
    {
        public string expressionId;
    }

    public struct VideoEvent {
        public string componentId;
        public string videoClipId;
        /** Status, can be NONE = 0, ERROR = 1, LOADING = 2, READY = 3, PLAYING = 4,BUFFERING = 5 */
        public int videoStatus;

        /** Current offset position in seconds */
        public float currentOffset;
        /** Video length in seconds. Can be -1 */
        public float totalVideoLength;
    }

    public struct ProfileChanged
    {
        public string ethAddress;
        public int version;
    }

    public struct PlayerConnected
    {
        public string userId;
    }

    public struct PlayerDisconnected
    {
        public string userId;
    }

    public struct OnRealmChanged
    {
        public string domain;
        public string room;
        public string serverName;
        public string displayName;
    }

    public struct PlayerClicked
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
