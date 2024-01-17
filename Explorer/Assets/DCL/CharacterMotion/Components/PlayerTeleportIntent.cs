using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerTeleportIntent
    {
        public readonly Vector2Int Parcel;
        public readonly Vector3 Position;

        public PlayerTeleportIntent(Vector3 position, Vector2Int parcel)
        {
            Position = position;
            Parcel = parcel;
        }
    }
}
