using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    // Edge slip mechanic
    // We utilize a downward spherecast to calculate in which part of our capsule we are hitting the ground, if its too far away from the threshold, we move towards the distance, making us move towards the edge
    public static class ApplyEdgeSlip
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            float dt,
            in ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            CharacterController characterController)
        {

            rigidTransform.IsOnASteepSlope = false;

            if (!rigidTransform.IsGrounded) return;

            Vector3 currentPosition = characterController.transform.position;

            // spherecast downwards to check edges
            Vector3 rayPosition = currentPosition + characterController.center;
            Vector3 rayDirection = Vector3.down * (characterController.height * 0.6f);

            if (!Physics.SphereCast(rayPosition, characterController.radius, rayDirection.normalized, out RaycastHit sphereCastHitInfo, characterController.height * 0.6f, PhysicsLayers.CHARACTER_ONLY_MASK))
            {
                ReduceSlopeTime(dt, settings, ref rigidTransform);
                return;
            }

            rigidTransform.CurrentSlopeNormal = sphereCastHitInfo.normal;

            Vector3 relativeHitPoint = sphereCastHitInfo.point - (currentPosition + characterController.center);
            relativeHitPoint.y = 0;

            // if the distance is not enough, we bail out
            if (!(relativeHitPoint.magnitude > settings.NoSlipDistance))
            {
                ReduceSlopeTime(dt, settings, ref rigidTransform);
                return;
            }

            Vector3 hitNormal = sphereCastHitInfo.normal;
            float angle = Vector3.Angle(Vector3.up, hitNormal);

            if (angle > characterController.slopeLimit)
                rigidTransform.IsOnASteepSlope = true;

            // raycast downwards to check if there's nothing, to avoid sliding on edges
            var groundRay = new Ray
            {
                origin = currentPosition,
                direction = Vector3.down,
            };

            // to avoid sliding on slopes, we added an additional raycast check
            if (!rigidTransform.IsOnASteepSlope && Physics.Raycast(groundRay, settings.EdgeSlipSafeDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
            {
                ReduceSlopeTime(dt, settings, ref rigidTransform);
                return;
            }

            // in order to slide correctly we change the gravity direction using the perpendicular direction of the normal based on the global up vector
            if (!rigidTransform.IsStuck)
                rigidTransform.GravityDirection = -Vector3.Cross(hitNormal, Vector3.Cross(Vector3.up, hitNormal)).normalized;

            AddSlopeTime(dt, settings, ref rigidTransform);
            rigidTransform.SteepSlopeAngle = angle;
        }

        private static void AddSlopeTime(float dt, ICharacterControllerSettings settings, ref CharacterRigidTransform rigidTransform)
        {
            rigidTransform.SteepSlopeTime = Mathf.Clamp(rigidTransform.SteepSlopeTime + dt, 0, settings.SlopeCharacterRotationDelay + 0.1f);
        }

        private static void ReduceSlopeTime(float dt, ICharacterControllerSettings settings, ref CharacterRigidTransform rigidTransform)
        {
            rigidTransform.SteepSlopeTime = Mathf.Clamp(rigidTransform.SteepSlopeTime - dt, 0, settings.SlopeCharacterRotationDelay + 0.1f);
        }
    }
}
