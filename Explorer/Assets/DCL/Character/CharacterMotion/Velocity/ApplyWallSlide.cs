using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyWallSlide
    {
        // We apply a multiplier to the current speed based on the dot product of the forward direction and the normal of the wall,
        // to get this normal we do a sphere cast forward form our character center using the characterController radius
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterRigidTransform rigidTransform, CharacterController characterController, in ICharacterControllerSettings settings)
        {
            if (!rigidTransform.IsGrounded || !rigidTransform.IsCollidingWithWall)
                return;

            if (rigidTransform.IsOnASteepSlope && rigidTransform.SteepSlopeAngle > settings.MaxSlopeAngle)
                return;

            Transform transform = characterController.transform;

            // To avoid doing the capsule cast from inside a mesh we reduce the size just a bit
            Vector3 smallOffset = Vector3.up * 0.1f;
            Vector3 point1 = transform.position + smallOffset;
            Vector3 point2 = point1 + (Vector3.up * characterController.height) - smallOffset;

            if (!Physics.CapsuleCast(
                    point1,
                    point2,
                    characterController.radius - 0.09f, // radius reduction to avoid casting from inside meshes
                    transform.forward,
                    out RaycastHit hit,
                    settings.WallSlideDetectionDistance,
                    PhysicsLayers.CHARACTER_ONLY_MASK))
                return;

            Vector3 hitInfoNormal = hit.normal;
            hitInfoNormal.y = 0;
            float dot = Mathf.Abs(Vector3.Dot(transform.forward, hitInfoNormal.normalized));
            float moveSpeedMultiplier = Mathf.Lerp(1f, settings.WallSlideMaxMoveSpeedMultiplier, dot);
            rigidTransform.MoveVelocity.Velocity *= moveSpeedMultiplier;
        }
    }
}
