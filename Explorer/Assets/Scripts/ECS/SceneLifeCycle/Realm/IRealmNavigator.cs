using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmNavigator
    {
        public const string WORLDS_DOMAIN = "https://worlds-content-server.decentraland.org/world";
        public const string LOCALHOST = "http://127.0.0.1:8000";

        public const string GOERLI_OLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main";
        public const string GOERLI_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest";

        public const string STREAM_WORLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/streaming-world-main";
        public const string SDK_TEST_SCENES_URL = "https://sdk-team-cdn.decentraland.org/ipfs/sdk7-test-scenes-main-latest";
        public const string TEST_SCENES_URL = "https://sdk-test-scenes.decentraland.zone";

        UniTask<Result> TryChangeRealmAsync(URLDomain realm, CancellationToken ct,
            Vector2Int parcelToTeleport = default);

        bool CheckIsNewRealm(URLDomain realm);

        UniTask<bool> CheckRealmIsReacheableAsync(URLDomain realm, CancellationToken ct);

        UniTask<Result> TryInitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct,
            bool isLocal = false, bool forceChangeRealm = false);

        UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport, CancellationToken ct, Vector2Int parcelToTeleport = default);

        UniTask LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct);

        void SwitchMiscVisibilityAsync();

        UniTask ChangeRealmAsync(URLDomain realm, CancellationToken ct);

        UniTask<UniTask> TeleportToParcelAsync(Vector2Int parcel, AsyncLoadProcessReport processReport,
            CancellationToken ct);

        // True if changed to GenesisCity, False - when changed to any other realm
        event Action<bool> RealmChanged;
    }
}
