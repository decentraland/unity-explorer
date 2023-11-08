using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplySlopeModifier
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Execute(
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent input,
            in JumpInputComponent jump,
            CharacterController characterController,
            float dt)
        {
            // disabled when jumping or not grounded
            if (!rigidTransform.IsGrounded || jump.IsPressed)
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
