using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public interface ITeleportController
    {
        UniTask TeleportToSceneSpawnPointAsync(Vector2Int parcel, CancellationToken ct);

        void TeleportToParcel(Vector2Int parcel);
    }
}
