using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Realm
{
    public interface IRealmNavigator
    {
        UniTask ChangeRealmAsync(string realm, CancellationToken ct);

        UniTask TeleportToParcelAsync(Vector2Int parcel, CancellationToken ct);
    }
}
