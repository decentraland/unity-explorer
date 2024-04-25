using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmNavigator
    {
        public const string GENESIS_URL = "https://peer.decentraland.org";
        public const string WORLDS_DOMAIN = "https://worlds-content-server.decentraland.org/world";
        public const string LOCALHOST = "http://127.0.0.1:8000";

        public const string GOERLI_URL = "https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main";
        public const string STREAM_WORLD_URL = "https://sdk-team-cdn.decentraland.org/ipfs/streaming-world-main";
        public const string SDK_TEST_SCENES_URL = "https://sdk-team-cdn.decentraland.org/ipfs/sdk7-test-scenes-main-latest";
        public const string TEST_SCENES_URL = "https://sdk-test-scenes.decentraland.zone";

        UniTask<bool> TryChangeRealmAsync(URLDomain realm, CancellationToken ct, bool terrainRegen = true);

        UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct);
    }
}
