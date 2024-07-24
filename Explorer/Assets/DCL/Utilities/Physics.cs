using UnityEngine;

namespace DCL.Utilities
{
    /// <summary>
    /// Decorator over Unity's Physics with Debug gizmos info.
    /// </summary>
    public static class Physics
    {
        public static bool SphereCast(Ray ray, float radius, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            bool hasHit = UnityEngine.Physics.SphereCast(ray, radius, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
            // if (Debug.isDebugBuild) DebugUtils.DrawRaycast(radius, ray.origin, maxDistance, hasHit, hitInfo);
            return hasHit;
        }

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            bool hasHit = UnityEngine.Physics.Raycast(ray, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
            if (Debug.isDebugBuild) DebugUtils.DrawRaycast(0.1f, ray.origin, maxDistance, hasHit, hitInfo);
            return hasHit;
        }
    }
}
