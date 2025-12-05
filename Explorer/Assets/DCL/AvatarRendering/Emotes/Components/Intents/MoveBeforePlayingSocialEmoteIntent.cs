using Arch.Core;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///
    /// </summary>
    public struct MoveBeforePlayingSocialEmoteIntent
    {
        /// <summary>
        ///
        /// </summary>
        public readonly Vector3 InitiatorWorldPosition;

        /// <summary>
        ///
        /// </summary>
        public readonly Quaternion InitiatorWorldRotation;

        /// <summary>
        ///
        /// </summary>
        public readonly Entity InitiatorEntityId;

        /// <summary>
        ///
        /// </summary>
        public readonly TriggerEmoteReactingToSocialEmoteIntent TriggerEmoteIntent;

        /// <summary>
        /// Indicates whether avatars are already looking at each other or not. This will be changed in a system.
        /// </summary>
        public bool AreAvatarsLookingAtEachOther;

        /// <summary>
        ///
        /// </summary>
        public bool HasBeenCancelled;

        /// <summary>
        ///
        /// </summary>
        public float StartTime;

        public MoveBeforePlayingSocialEmoteIntent(Vector3 initiatorWorldPosition, Quaternion initiatorWorldRotation, Entity initiatorEntityId, TriggerEmoteReactingToSocialEmoteIntent triggerEmoteIntent)
        {
            InitiatorWorldPosition = initiatorWorldPosition;
            InitiatorWorldRotation = initiatorWorldRotation;
            TriggerEmoteIntent = triggerEmoteIntent;
            InitiatorEntityId = initiatorEntityId;
            AreAvatarsLookingAtEachOther = false;
            HasBeenCancelled = false;
            StartTime = Time.time;
        }
    }
}
