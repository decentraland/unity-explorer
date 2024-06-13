using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public readonly struct PlayerLookAtIntent
    {
        public readonly Vector3 LookAtTarget;
        public readonly Vector3? From;

        public PlayerLookAtIntent(Vector3 lookAtTarget, Vector3? from = null)
        {
            LookAtTarget = lookAtTarget;
            From = from;
        }
    }
}
