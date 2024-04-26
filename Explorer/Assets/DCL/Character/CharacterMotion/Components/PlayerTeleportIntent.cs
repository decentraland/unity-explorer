using DCL.AsyncLoadReporting;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerTeleportIntent
    {
        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;
        public readonly AsyncLoadProcessReport? LoadReport;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel, AsyncLoadProcessReport? loadReport = null)
        {
            Position = position;
            Parcel = parcel;
            LoadReport = loadReport;
        }
    }
}
