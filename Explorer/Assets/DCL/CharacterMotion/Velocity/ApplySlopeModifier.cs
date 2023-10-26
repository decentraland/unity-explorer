using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplySlopeModifier
    {
        public static Vector3 Execute(in CharacterRigidTransform rigidTransform,
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

            // Todo: cache
            var ray = new Ray
            {
                origin = position + (rigidTransform.MoveVelocity.Velocity * dt),
                direction = Vector3.down,
            };

            float downwardsSlopeDistance = input.Kind == MovementKind.Run ? 0.55f : 0.45f;

            if (!Physics.Raycast(ray, out RaycastHit hit, downwardsSlopeDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
                return Vector3.zero;

            float diff = feet - hit.point.y;

            return Vector3.down * diff;
        }
    }
}
