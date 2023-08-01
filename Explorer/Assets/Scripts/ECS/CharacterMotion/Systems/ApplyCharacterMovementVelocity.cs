using CrdtEcsBridge.Components.Special;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.CharacterMotion.Systems
{
    public static class ApplyCharacterMovementVelocity
    {
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterPhysics characterPhysics,
            in CameraComponent camera,
            in MovementInputComponent input,
            float deltaTime)
        {
            Transform cameraTransform = camera.Camera.transform;
            Vector3 forward = cameraTransform.forward;
            forward.y = 0;
            Vector3 right = cameraTransform.right;
            right.y = 0;

            Vector3 targetForward = ((forward * input.Axes.y) + (right * input.Axes.x)).normalized;
            Vector3 targetVelocity = targetForward * GetSpeedLimit(characterControllerSettings, input);

            // Interpolate velocity
            float acceleration = GetAcceleration(characterControllerSettings, characterPhysics);
            characterPhysics.Velocity = Vector3.MoveTowards(characterPhysics.Velocity, targetVelocity, acceleration * deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetAcceleration(ICharacterControllerSettings characterControllerSettings, in CharacterPhysics physics) =>
            physics.IsGrounded ? characterControllerSettings.GroundAcceleration : characterControllerSettings.AirAcceleration;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetSpeedLimit(ICharacterControllerSettings characterControllerSettings, in MovementInputComponent inputComponent)
        {
            switch (inputComponent.Kind)
            {
                case MovementKind.Run:
                    return characterControllerSettings.RunSpeed;
                default: return characterControllerSettings.WalkSpeed;
            }
        }
    }
}
