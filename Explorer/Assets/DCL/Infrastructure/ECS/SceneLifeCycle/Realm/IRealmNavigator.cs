using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace ECS.SceneLifeCycle.Realm
{
    public enum ChangeRealmError
    {
        MessageError,
        ChangeCancelled,
        SameRealm,
        NotReachable,
    }

    public static class ChangeRealmErrors
    {
        public static TaskError AsTaskError(this ChangeRealmError e) =>
            e switch
            {
                ChangeRealmError.MessageError => TaskError.MessageError,
                ChangeRealmError.ChangeCancelled => TaskError.Cancelled,
                ChangeRealmError.SameRealm => TaskError.MessageError,
                ChangeRealmError.NotReachable => TaskError.MessageError,
                _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
            };

        public static ChangeRealmError AsChangeRealmError(this TaskError e) =>
            e switch
            {
                TaskError.MessageError => ChangeRealmError.MessageError,
                TaskError.Timeout => ChangeRealmError.MessageError,
                TaskError.Cancelled => ChangeRealmError.ChangeCancelled,
                TaskError.UnexpectedException => ChangeRealmError.MessageError,
                _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
            };

        public static bool IsRecoverable(this ChangeRealmError error) =>
            error is ChangeRealmError.SameRealm or ChangeRealmError.NotReachable;
    }

    public interface IRealmNavigator
    {
        public static readonly Uri LOCALHOST = new ("http://127.0.0.1:8000");

        public static readonly Uri GOERLI_OLD_URL = new ("https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main");
        public static readonly Uri GOERLI_URL = new ("https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest");

        public static readonly Uri STREAM_WORLD_URL = new ("https://sdk-team-cdn.decentraland.org/ipfs/streaming-world-main");
        public static readonly Uri SDK_TEST_SCENES_URL = new ("https://sdk-team-cdn.decentraland.org/ipfs/sdk7-test-scenes-main-latest");
        public static readonly Uri TEST_SCENES_URL = new ("https://sdk-test-scenes.decentraland.zone");

        event Action<Vector2Int> NavigationExecuted;

        UniTask<EnumResult<ChangeRealmError>> TryChangeRealmAsync(
            URLDomain realm,
            CancellationToken ct,
            Vector2Int parcelToTeleport = default
        );

        UniTask<EnumResult<TaskError>> TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isLocal);

        void RemoveCameraSamplingData();
    }
}
