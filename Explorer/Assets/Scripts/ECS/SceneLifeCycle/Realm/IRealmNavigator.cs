using System;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;
using DCL.AsyncLoadReporting;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmNavigator
    {
        public const string GENESIS_URL = "https://peer.decentraland.org";
        public const string WORLDS_DOMAIN = "https://worlds-content-server.decentraland.org/world";
        public const string LOCALHOST = "http://127.0.0.1:8000";

        public const string GOERLI_OLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main";
        public const string GOERLI_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest";

        public const string STREAM_WORLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/streaming-world-main";
        public const string SDK_TEST_SCENES_URL = "https://sdk-team-cdn.decentraland.org/ipfs/sdk7-test-scenes-main-latest";
        public const string TEST_SCENES_URL = "https://sdk-test-scenes.decentraland.zone";

        public const string GOERLI_CONTENT_URL = "https://sdk-team-cdn.decentraland.org/ipfs/";
        public const string GENESIS_CONTENT_URL = "https://peer.decentraland.org/content/contents/";
        public const string WORLDS_CONTENT_URL = "https://worlds-content-server.decentraland.org/contents/";

        URLDomain CurrentRealm { get; }

        UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct, bool isSoloSceneLoading, Vector2Int parcelToTeleport = default);

        UniTask TryInitializeTeleportToParcelAsync(Vector2Int parcel, CancellationToken ct, bool isSoloSceneLoading, bool isLocal = false);

        UniTask InitializeTeleportToSpawnPointAsync(AsyncLoadProcessReport teleportLoadReport, CancellationToken ct, Vector2Int parcelToTeleport = default);

        UniTask LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct);

        UniTask SwitchMiscVisibilityAsync();

        // True if changed to GenesisCity, False - when changed to any other realm
        event Action<bool> RealmChanged;
    }
}
