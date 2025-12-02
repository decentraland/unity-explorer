
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    /// <summary>
    ///
    /// </summary>
    public struct InterpolateToOutcomeStartPoseIntent
    {
        /// <summary>
        ///
        /// </summary>
        public readonly Vector3 TargetAvatarPosition;

        /// <summary>
        ///
        /// </summary>
        public readonly Quaternion TargetAvatarRotation;

        /// <summary>
        ///
        /// </summary>
        public readonly Vector3 OriginalAvatarPosition;

        /// <summary>
        ///
        /// </summary>
        public readonly Quaternion OriginalAvatarRotation;

        /// <summary>
        ///
        /// </summary>
        public readonly TriggerEmoteReactingToSocialEmoteIntent TriggerEmoteIntent;

        /// <summary>
        ///
        /// </summary>
        public readonly float MovementStartTime;

        /// <summary>
        ///
        /// </summary>
        public readonly Vector3 InitiatorWorldPosition;

        /// <summary>
        ///
        /// </summary>
        public bool HasBeenCancelled;

        /// <summary>
        ///
        /// </summary>
        /// <param name="originalAvatarPosition"></param>
        /// <param name="originalAvatarRotation"></param>
        /// <param name="targetAvatarPosition"></param>
        /// <param name="targetAvatarRotation"></param>
        /// <param name="triggerEmoteIntent"></param>
        /// <param name="initiatorWorldPosition"></param>
        public InterpolateToOutcomeStartPoseIntent(Vector3 originalAvatarPosition, Quaternion originalAvatarRotation, Vector3 targetAvatarPosition, Quaternion targetAvatarRotation, TriggerEmoteReactingToSocialEmoteIntent triggerEmoteIntent, Vector3 initiatorWorldPosition)
        {
            TargetAvatarPosition = targetAvatarPosition;
            TargetAvatarRotation = targetAvatarRotation;
            TriggerEmoteIntent = triggerEmoteIntent;
            OriginalAvatarPosition = originalAvatarPosition;
            OriginalAvatarRotation = originalAvatarRotation;
            MovementStartTime = Time.time;
            InitiatorWorldPosition = initiatorWorldPosition;
            HasBeenCancelled = false;
        }
    }
}
