using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.Utilities
{
    public class DebugUtils
    {
        [Conditional("UNITY_EDITOR")]
        public static void DrawRaycast(float radius, Vector3 rayOrigin, float rayDistance, bool hasHit, RaycastHit hitInfo)
        {
            // Draw the ray
            Debug.DrawRay(rayOrigin, Vector3.down * (rayDistance + radius), Color.yellow);

            // Draw the sphere at the start and end of the raycast
            DrawSphere(rayOrigin, radius, Color.green);
            DrawSphere(rayOrigin + (Vector3.down * (rayDistance + radius)), radius, Color.red);

            if (hasHit)
            {
                // Draw a line to the hit point
                Debug.DrawLine(rayOrigin, hitInfo.point, Color.blue);

                // Draw the sphere at the hit point
                DrawSphere(hitInfo.point, radius, Color.magenta);
            }
        }

#if UNITY_EDITOR
        public static void DrawSphere(Vector3 center, float radius, Color color)
        {
            // Draw three circles to represent the sphere
            DrawCircle(center, radius, color, Vector3.forward);
            DrawCircle(center, radius, color, Vector3.right);
            DrawCircle(center, radius, color, Vector3.up);
        }

        public static void DrawCircle(Vector3 center, float radius, Color color, Vector3 normal, int segments = 16)
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
