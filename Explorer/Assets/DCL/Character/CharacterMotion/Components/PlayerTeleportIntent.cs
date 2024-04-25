using DCL.AsyncLoadReporting;
using JetBrains.Annotations;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerTeleportIntent
    {
        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;
        [CanBeNull] public readonly AsyncLoadProcessReport LoadReport;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel, [CanBeNull] AsyncLoadProcessReport loadReport = null)
        {
            Position = position;
            Parcel = parcel;
            LoadReport = loadReport;
        }
    }
}
