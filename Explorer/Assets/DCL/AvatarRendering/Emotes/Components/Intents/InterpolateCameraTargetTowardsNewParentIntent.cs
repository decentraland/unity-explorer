using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    /// Add this component to smoothly move the Camera Focus object of an avatar to the position of another parent, keeping the same relative height.
    /// </summary>
    public readonly struct InterpolateCameraTargetTowardsNewParentIntent
    {
        /// <summary>
        /// The position of the object when the interpolation started.
        /// </summary>
        public readonly Vector3 StartPosition;

        /// <summary>
        /// The instant when the interpolation started.
        /// </summary>
        public readonly float StartTime;

        /// <summary>
        /// The position of the object will move towards this target.
        /// </summary>
        public readonly Transform Target;

        /// <summary>
        /// The local height of the object with respect to the target.
        /// </summary>
        public readonly float LocalHeight;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="startPosition">The position of the object when the interpolation started.</param>
        /// <param name="target">The position of the object will move towards this target.</param>
        /// <param name="localHeight">The local height of the object with respect to the target.</param>
        public InterpolateCameraTargetTowardsNewParentIntent(Vector3 startPosition, Transform target, float localHeight)
        {
            StartPosition = startPosition;
            Target = target;
            LocalHeight = localHeight;
            StartTime = Time.time;
        }
    }
}
