
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
        /// The position of the avatar when the interpolation started.
        /// </summary>
        public readonly Vector3 OriginalAvatarPosition;

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
        /// <param name="initiatorWorldPosition">The world position of the initiator's avatar.</param>
        public InterpolateToOutcomeStartPoseIntent(Vector3 originalAvatarPosition, Vector3 initiatorWorldPosition)
        {
            OriginalAvatarPosition = originalAvatarPosition;
            MovementStartTime = Time.time;
            InitiatorWorldPosition = initiatorWorldPosition;
            HasBeenCancelled = false;
        }
    }
}
