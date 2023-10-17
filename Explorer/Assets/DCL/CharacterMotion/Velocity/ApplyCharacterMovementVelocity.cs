using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyCharacterMovementVelocity
    {
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform.MovementVelocity movementVelocity,
            in CameraComponent camera,
            in MovementInputComponent input)
        {
            Transform cameraTransform = camera.Camera.transform;
            Vector3 forward = cameraTransform.forward;
            forward.y = 0;
            Vector3 right = cameraTransform.right;
            right.y = 0;

            Vector3 targetForward = ((forward * input.Axes.y) + (right * input.Axes.x)).normalized;
            Vector3 targetVelocity = targetForward * GetSpeedLimit(characterControllerSettings, input);
            movementVelocity.Target = targetVelocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetSpeedLimit(ICharacterControllerSettings characterControllerSettings, in MovementInputComponent inputComponent)
        {
            switch (inputComponent.Kind)
            {
                case MovementKind.Run:
                    return characterControllerSettings.RunSpeed;
                case MovementKind.Jog:
                    return characterControllerSettings.JogSpeed;
                default: return characterControllerSettings.WalkSpeed;
            }
        }
    }
}
