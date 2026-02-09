using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyExternalVelocityDrag
    {
        private const float MIN_VELOCITY_THRESHOLD = 0.01f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            float deltaTime)
        {
            // Reset vertical component when grounded (like gravity)
            if (characterPhysics.IsGrounded && characterPhysics.ExternalVelocity.y < 0)
                characterPhysics.ExternalVelocity.y = 0;

            if (characterPhysics.ExternalVelocity.sqrMagnitude < MIN_VELOCITY_THRESHOLD * MIN_VELOCITY_THRESHOLD)
            {
                characterPhysics.ExternalVelocity = Vector3.zero;
                return;
            }

            // Split into vertical and horizontal components
            float verticalVelocity = characterPhysics.ExternalVelocity.y;
            Vector3 horizontalVelocity = new Vector3(
                characterPhysics.ExternalVelocity.x,
                0,
                characterPhysics.ExternalVelocity.z);

            if (characterPhysics.IsGrounded)
            {
                // On ground: apply friction to horizontal, zero out vertical
                horizontalVelocity = ApplyFriction(horizontalVelocity, settings.ExternalFriction, deltaTime);
                verticalVelocity = 0;
            }
            else
            {
                // In air: apply air drag to all components
                horizontalVelocity = ApplyDrag(horizontalVelocity, settings.ExternalAirDrag, deltaTime);
                verticalVelocity = ApplyDragScalar(verticalVelocity, settings.ExternalAirDrag, deltaTime);
            }

            characterPhysics.ExternalVelocity = new Vector3(
                horizontalVelocity.x,
                verticalVelocity,
                horizontalVelocity.z);

            // Clamp to max velocity
            if (characterPhysics.ExternalVelocity.sqrMagnitude > settings.MaxExternalVelocity * settings.MaxExternalVelocity)
                characterPhysics.ExternalVelocity = characterPhysics.ExternalVelocity.normalized * settings.MaxExternalVelocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ApplyDrag(Vector3 velocity, float drag, float deltaTime)
        {
            // Quadratic drag: F = -drag * v^2 * direction
            float speed = velocity.magnitude;
            if (speed < MIN_VELOCITY_THRESHOLD) return Vector3.zero;

            float dragForce = drag * speed * speed;
            float newSpeed = Mathf.Max(0, speed - dragForce * deltaTime);
            return velocity.normalized * newSpeed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApplyDragScalar(float velocity, float drag, float deltaTime)
        {
            float speed = Mathf.Abs(velocity);
            if (speed < MIN_VELOCITY_THRESHOLD) return 0;

            float dragForce = drag * speed * speed;
            float newSpeed = Mathf.Max(0, speed - dragForce * deltaTime);
            return Mathf.Sign(velocity) * newSpeed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ApplyFriction(Vector3 velocity, float friction, float deltaTime)
        {
            // Linear friction: v = v - friction * dt
            float speed = velocity.magnitude;
            if (speed < MIN_VELOCITY_THRESHOLD) return Vector3.zero;

            float newSpeed = Mathf.Max(0, speed - friction * deltaTime);
            return velocity.normalized * newSpeed;
        }
    }
}
