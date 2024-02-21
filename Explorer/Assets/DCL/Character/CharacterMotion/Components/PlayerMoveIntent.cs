using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerMoveIntent
    {
        public readonly Vector3 Position;

        public PlayerMoveIntent(Vector3 position)
        {
            Position = position;
        }
    }
}
