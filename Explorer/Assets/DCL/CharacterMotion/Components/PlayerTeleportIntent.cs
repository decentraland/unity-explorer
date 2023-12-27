using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct PlayerTeleportIntent
    {
        public Vector3 Position;

        public PlayerTeleportIntent(Vector3 position)
        {
            Position = position;
        }
    }
}
