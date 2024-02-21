using UnityEngine;

namespace DCL.CharacterCamera
{
    public readonly struct CameraLookAtIntent
    {
        public readonly Vector3 LookAtTarget;

        public CameraLookAtIntent(Vector3 lookAtTarget)
        {
            LookAtTarget = lookAtTarget;
        }
    }
}
