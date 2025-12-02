using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///
    /// </summary>
    public struct InterpolateCameraTargetTowardsNewParentIntent
    {
        /// <summary>
        ///
        /// </summary>
        public Vector3 StartPosition;

        /// <summary>
        ///
        /// </summary>
        public float StartTime;

        /// <summary>
        ///
        /// </summary>
        public Transform Target;

        /// <summary>
        ///
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="target"></param>
        public InterpolateCameraTargetTowardsNewParentIntent(Vector3 startPosition, Transform target)
        {
            StartPosition = startPosition;
            Target = target;
            StartTime = UnityEngine.Time.time;
        }
    }
}
