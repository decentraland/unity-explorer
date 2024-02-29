using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerLookAtIntent
    {
        public readonly Vector3 LookAtTarget;

        public PlayerLookAtIntent(Vector3 lookAtTarget)
        {
            LookAtTarget = lookAtTarget;
        }
    }
}
