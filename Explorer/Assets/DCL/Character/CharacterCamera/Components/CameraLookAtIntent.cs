using UnityEngine;

namespace DCL.CharacterCamera
{
    public readonly struct CameraLookAtIntent
    {
        public readonly Vector3 LookAtTarget;
        public readonly Vector3 PlayerPosition;

        public CameraLookAtIntent(Vector3 lookAtTarget, Vector3 playerPosition)
        {
            LookAtTarget = lookAtTarget;
            PlayerPosition = playerPosition;
        }
    }
}
