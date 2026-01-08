using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Utils;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Velocity
{
    public static class ApplyFirstPersonRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterRigidTransform rigidTransform, in CameraComponent cameraComponent)
        {
            Transform cameraTransform = cameraComponent.Camera.transform;

            Vector3 forward = cameraTransform.forward;
            Vector3 up = cameraTransform.up;

            rigidTransform.LookDirection = LookDirectionUtils.FlattenLookDirection(forward, up);
        }
    }
}
