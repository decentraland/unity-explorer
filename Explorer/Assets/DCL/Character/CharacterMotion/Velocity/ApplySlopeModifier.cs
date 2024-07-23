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

            Vector3 characterPosition = characterController.transform.position;
            float feet = characterPosition.y;

            Vector3 rayOrigin = characterPosition + (rigidTransform.MoveVelocity.Velocity * dt);
            var ray = new Ray
            {
                origin = rayOrigin,
                direction = Vector3.down,
            };

            float downwardsSlopeDistance = input.Kind == MovementKind.Run ? settings.DownwardsSlopeRunRaycastDistance : settings.DownwardsSlopeJogRaycastDistance;

            // Debug: Draw the ray
            Debug.DrawRay(ray.origin, ray.direction * downwardsSlopeDistance, Color.yellow);

            if (!Physics.Raycast(ray, out RaycastHit hit, downwardsSlopeDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
                return Vector3.zero;

            float diff = feet - hit.point.y;

            // Debug: Draw the hit point
            Debug.DrawLine(ray.origin, hit.point, Color.green);
            DebugDrawSphere(hit.point, 0.1f, Color.red);

            // Debug: Draw the vertical difference
            Debug.DrawLine(characterPosition, new Vector3(characterPosition.x, hit.point.y, characterPosition.z), Color.blue);

            Debug.Log($"VVV Slope Modifier: {diff}");
            return Vector3.down * diff;
        }

#if UNITY_EDITOR
        private static void DebugDrawSphere(Vector3 center, float radius, Color color)
        {
            // Draw three circles to represent the sphere
            DebugDrawCircle(center, radius, color, Vector3.forward);
            DebugDrawCircle(center, radius, color, Vector3.right);
            DebugDrawCircle(center, radius, color, Vector3.up);
        }

        private static void DebugDrawCircle(Vector3 center, float radius, Color color, Vector3 normal, int segments = 16)
        {
            Vector3 from = Vector3.zero;
            Vector3 to = Vector3.zero;

            for (var i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * 360 * Mathf.Deg2Rad;
                to.x = Mathf.Cos(angle);
                to.y = Mathf.Sin(angle);

                if (i > 0)
                {
                    Vector3 fromWorld = center + (Quaternion.LookRotation(normal) * from * radius);
                    Vector3 toWorld = center + (Quaternion.LookRotation(normal) * to * radius);
                    Debug.DrawLine(fromWorld, toWorld, color);
                }

                from = to;
            }
        }
#endif
    }
}
