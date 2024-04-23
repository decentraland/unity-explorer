using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Velocity
{
    public static class ApplyFirstPersonRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterRigidTransform rigidTransform, in CameraComponent cameraComponent)
        {
            Vector3 flatLookAt = cameraComponent.Camera.transform.forward;
            flatLookAt.y = 0;

            rigidTransform.LookDirection = flatLookAt.normalized;
        }
    }
}
