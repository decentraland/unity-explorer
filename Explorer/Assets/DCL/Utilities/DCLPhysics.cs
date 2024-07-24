using UnityEngine;

namespace DCL.Utilities
{
    /// <summary>
    /// Decorator over Unity's Physics with Debug gizmos info.
    /// </summary>
    public static class DCLPhysics
    {
        /// <summary>
        /// Unity SphereCast with Debug gizmos.
        /// </summary>
        public static bool SphereCast(Ray ray, float radius, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            bool hasHit = Physics.SphereCast(ray, radius, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
            // if (Application.isEditor) DebugUtils.DrawRaycast(radius, ray.origin, maxDistance, hasHit, hitInfo);
            return hasHit;
        }

        /// <summary>
        /// Unity Raycast with Debug gizmos.
        /// </summary>
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            bool hasHit = Physics.Raycast(ray, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
            if (Application.isEditor) DebugUtils.DrawRaycast(0.1f, ray.origin, maxDistance, hasHit, hitInfo);
            return hasHit;
        }
    }
}
