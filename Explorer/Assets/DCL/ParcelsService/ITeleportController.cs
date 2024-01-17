using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public interface ITeleportController
    {
        UniTask TeleportToSceneSpawnPointAsync(Vector2Int parcel, AsyncLoadProcessReport? loadReport, CancellationToken ct);

        void TeleportToParcel(Vector2Int parcel);
    }
}
