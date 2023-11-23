using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyWallSlide
    {
        public static void Execute(ref CharacterRigidTransform rigidTransform, CharacterController characterController)
        {
            if (!rigidTransform.IsGrounded || !rigidTransform.IsCollidingWithWall)
                return;

            Transform transform = characterController.transform;

            Vector3 position = transform.position;
            position += characterController.center;

            var ray = new Ray
            {
                origin = position,
                direction = transform.forward,
            };

            if (!Physics.SphereCast(ray, characterController.radius, out RaycastHit hit, 0.5f, PhysicsLayers.CHARACTER_ONLY_MASK)) return;

            float dot = Mathf.Abs(Vector3.Dot(transform.forward, hit.normal));
            float moveSpeedMultiplier = Mathf.Lerp(1f, 0.3f, dot);
            rigidTransform.MoveVelocity.Velocity *= moveSpeedMultiplier;
        }
    }
}
