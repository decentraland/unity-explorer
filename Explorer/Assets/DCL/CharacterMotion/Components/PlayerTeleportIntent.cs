using SceneRunner.Scene;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerTeleportIntent
    {
        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;
        public readonly SceneReadinessReport? SceneReadyReport;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel, SceneReadinessReport? sceneReadyReport)
        {
            Position = position;
            SceneReadyReport = sceneReadyReport;
            Parcel = parcel;
        }
    }
}
