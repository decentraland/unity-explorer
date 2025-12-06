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
        public float LocalHeight;

        /// <summary>
        ///
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="target"></param>
        /// <param name="localHeight"></param>
        public InterpolateCameraTargetTowardsNewParentIntent(Vector3 startPosition, Transform target, float localHeight)
        {
            StartPosition = startPosition;
            Target = target;
            LocalHeight = localHeight;
            StartTime = UnityEngine.Time.time;
        }
    }
}
