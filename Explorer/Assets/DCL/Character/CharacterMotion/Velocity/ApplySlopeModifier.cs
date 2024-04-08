using CrdtEcsBridge.Physics;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplySlopeModifier
    {
        // This function returns the height modifier of the velocity for the character to stick to downward slopes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Execute(
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent input,
            in JumpInputComponent jump,
            CharacterController characterController,
            float dt)
        {
            // before moving we check if we are able to step up
            characterController.stepOffset = settings.StepOffset;

            // disabled when jumping or not grounded
            if (!rigidTransform.IsGrounded || jump.IsPressed || rigidTransform.IsOnASteepSlope)
                return Vector3.zero;

            Vector3 position = characterController.transform.position;
            float feet = position.y;
            position.y = feet;

            var ray = new Ray
            {
                origin = position + (rigidTransform.MoveVelocity.Velocity * dt),
                direction = Vector3.down,
            };

            float downwardsSlopeDistance = input.Kind == MovementKind.Run ? settings.DownwardsSlopeRunRaycastDistance : settings.DownwardsSlopeJogRaycastDistance;

            if (!Physics.Raycast(ray, out RaycastHit hit, downwardsSlopeDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
                return Vector3.zero;

            float diff = feet - hit.point.y;

            return Vector3.down * diff;
        }
    }
}
