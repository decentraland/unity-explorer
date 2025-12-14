using Arch.Core;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     This component is added when a player reacts to the social emote being played by other avatar, so the reacting
    ///     avatar moves to reach it.
    /// </summary>
    public struct MoveBeforePlayingSocialEmoteIntent
    {
        /// <summary>
        ///     The world position of the initiator's avatar.
        /// </summary>
        public readonly Vector3 InitiatorWorldPosition;

        /// <summary>
        ///     The entity ID of the initiator's avatar.
        /// </summary>
        public readonly Entity InitiatorEntityId;

        /// <summary>
        ///     The data sent by the player when reacted to the social emote.
        /// </summary>
        public readonly TriggerEmoteReactingToSocialEmoteIntent TriggerEmoteIntent;

        /// <summary>
        ///     The instant when the reaction occurred. Used to check for a timeout.
        /// </summary>
        public readonly float StartTime;

        /// <summary>
        ///     Indicates whether avatars are already looking at each other or not. This will be changed in a system.
        /// </summary>
        public bool AreAvatarsLookingAtEachOther;

        /// <summary>
        ///     Set when the movement has to be cancelled due to causes that are not moving, jumping or emoting.
        /// </summary>
        public bool HasBeenCancelled;

        /// <summary>
        ///     Default constructor.
        /// </summary>
        /// <param name="initiatorWorldPosition">The world position of the initiator's avatar.</param>
        /// <param name="initiatorEntityId">The entity ID of the initiator's avatar.</param>
        /// <param name="triggerEmoteIntent">The data sent by the player when reacted to the social emote.</param>
        public MoveBeforePlayingSocialEmoteIntent(Vector3 initiatorWorldPosition, Entity initiatorEntityId, TriggerEmoteReactingToSocialEmoteIntent triggerEmoteIntent)
        {
            InitiatorWorldPosition = initiatorWorldPosition;
            TriggerEmoteIntent = triggerEmoteIntent;
            InitiatorEntityId = initiatorEntityId;
            AreAvatarsLookingAtEachOther = false;
            HasBeenCancelled = false;
            StartTime = Time.time;
        }
    }
}