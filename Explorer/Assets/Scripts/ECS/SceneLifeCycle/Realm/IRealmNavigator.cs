using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmNavigator
    {
        public const string GENESIS_URL = "https://peer.decentraland.org";
        public const string WORLDS_DOMAIN = "https://worlds-content-server.decentraland.org/world";

        UniTask<bool> TryChangeRealmAsync(string realm, CancellationToken ct);

        UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct);
    }
}
