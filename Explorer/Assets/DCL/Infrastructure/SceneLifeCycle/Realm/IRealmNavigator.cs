using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public enum ChangeRealmError
    {
        MESSAGE_ERROR,
        CHANGE_CANCELLED,
        NOT_REACHABLE,
        LOCAL_SCENE_DEVELOPMENT_BLOCKED,
        UNAUTHORIZED_WORLD_ACCESS,
        TIMEOUT,

        /// <summary>
        /// World requires a password to access.
        /// </summary>
        PASSWORD_REQUIRED,

        /// <summary>
        /// User cancelled the password entry.
        /// </summary>
        PASSWORD_CANCELLED,

        /// <summary>
        /// User is not on the allow-list for this world.
        /// </summary>
        WHITELIST_ACCESS_DENIED
    }

    public static class ChangeRealmErrors
    {
        public static TaskError AsTaskError(this ChangeRealmError e) =>
            e switch
            {
                ChangeRealmError.MESSAGE_ERROR => TaskError.MessageError,
                ChangeRealmError.CHANGE_CANCELLED => TaskError.Cancelled,
                ChangeRealmError.NOT_REACHABLE => TaskError.MessageError,
                ChangeRealmError.LOCAL_SCENE_DEVELOPMENT_BLOCKED => TaskError.MessageError,
                ChangeRealmError.TIMEOUT => TaskError.Timeout,
                ChangeRealmError.PASSWORD_REQUIRED => TaskError.MessageError,
                ChangeRealmError.PASSWORD_CANCELLED => TaskError.Cancelled,
                ChangeRealmError.WHITELIST_ACCESS_DENIED => TaskError.MessageError,
                ChangeRealmError.UNAUTHORIZED_WORLD_ACCESS => TaskError.MessageError,
                _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
            };

        public static ChangeRealmError AsChangeRealmError(this TaskError e) =>
            e switch
            {
                TaskError.MessageError => ChangeRealmError.MESSAGE_ERROR,
                TaskError.Timeout => ChangeRealmError.TIMEOUT,
                TaskError.Cancelled => ChangeRealmError.CHANGE_CANCELLED,
                TaskError.UnexpectedException => ChangeRealmError.MESSAGE_ERROR,
                _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
            };

    }

    public interface IRealmNavigator
    {
        public const string LOCALHOST = "http://127.0.0.1:8000";

        public const string GOERLI_OLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main";
        public const string GOERLI_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest";

        public const string STREAM_WORLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/streaming-world-main";
        public const string SDK_TEST_SCENES_URL = "https://sdk-team-cdn.decentraland.org/ipfs/sdk7-test-scenes-main-latest";
        public const string TEST_SCENES_URL = "https://sdk-test-scenes.decentraland.zone";

        event Action<Vector2Int> NavigationExecuted;

        UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(
            URLDomain realm,
            CancellationToken ct,
            Vector2Int parcelToTeleport = default,
            bool isWorld = false,
            bool allowsSpawnPointerOverride = false,
            bool landOnParcel = false,
            string? spawnPointName = null
        );

        UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal, bool landOnParcel = false, string? spawnPointName = null);

        bool IsAlreadyOnRealm(URLDomain realm);

        void RemoveCameraSamplingData();
    }
}
