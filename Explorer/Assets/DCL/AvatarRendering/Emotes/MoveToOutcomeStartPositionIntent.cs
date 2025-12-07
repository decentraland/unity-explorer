
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    /// <summary>
    /// Component added when an avatar is reacting to a social emote and is close enough to the other so it can start playing the emote. The avatar will be smoothly moved
    /// to the position the avatar will be at when the outcome animation of the social emote starts. While the avatar is moving, the CharacterController stays where it was.
    /// </summary>
    public struct InterpolateToOutcomeStartPoseIntent
    {
        /// <summary>
        /// Where the avatar will end up at the end of the interpolation.
        /// </summary>
        public readonly Vector3 TargetAvatarPosition;

        /// <summary>
        /// What rotation the avatar will have at the end of the interpolation.
        /// </summary>
        public readonly Quaternion TargetAvatarRotation;

        /// <summary>
        /// The position of the avatar when the interpolation started.
        /// </summary>
        public readonly Vector3 OriginalAvatarPosition;

        /// <summary>
        /// The rotation of the avatar when the interpolation started.
        /// </summary>
        public readonly Quaternion OriginalAvatarRotation;

        /// <summary>
        /// The instant when the interpolation started.
        /// </summary>
        public readonly float MovementStartTime;

        /// <summary>
        /// The world position of the initiator's avatar.
        /// </summary>
        public readonly Vector3 InitiatorWorldPosition;

        /// <summary>
        /// Set when the interpolation has to be stopped. An avatar may have moved, jumped or there is any situation that cancels the emote.
        /// </summary>
        public bool HasBeenCancelled;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="originalAvatarPosition">The position of the avatar when the interpolation started.</param>
        /// <param name="originalAvatarRotation">The rotation of the avatar when the interpolation started.</param>
        /// <param name="targetAvatarPosition">Where the avatar will end up at the end of the interpolation.</param>
        /// <param name="targetAvatarRotation">What rotation the avatar will have at the end of the interpolation.</param>
        /// <param name="initiatorWorldPosition">The world position of the initiator's avatar.</param>
        public InterpolateToOutcomeStartPoseIntent(Vector3 originalAvatarPosition, Quaternion originalAvatarRotation, Vector3 targetAvatarPosition, Quaternion targetAvatarRotation, Vector3 initiatorWorldPosition)
        {
            TargetAvatarPosition = targetAvatarPosition;
            TargetAvatarRotation = targetAvatarRotation;
            OriginalAvatarPosition = originalAvatarPosition;
            OriginalAvatarRotation = originalAvatarRotation;
            MovementStartTime = Time.time;
            InitiatorWorldPosition = initiatorWorldPosition;
            HasBeenCancelled = false;
        }
    }
}
