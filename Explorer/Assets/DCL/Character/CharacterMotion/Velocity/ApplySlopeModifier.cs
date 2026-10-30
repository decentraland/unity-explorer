using CrdtEcsBridge.Physics;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Utilities;
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

            Vector3 characterPosition = characterController.transform.position;
            float feet = characterPosition.y;

            Vector3 rayOrigin = characterPosition + (rigidTransform.MoveVelocity.Velocity * dt);
            var ray = new Ray
            {
                origin = rayOrigin,
                direction = Vector3.down,
            };

            float downwardsSlopeDistance = input.Kind == MovementKind.Run ? settings.DownwardsSlopeRunRaycastDistance : settings.DownwardsSlopeJogRaycastDistance;

            if (!DCLPhysics.Raycast(ray, out RaycastHit hit, downwardsSlopeDistance, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore))
                return Vector3.zero;

            float diff = feet - hit.point.y;

            return Vector3.down * diff;
        }
    }
}
