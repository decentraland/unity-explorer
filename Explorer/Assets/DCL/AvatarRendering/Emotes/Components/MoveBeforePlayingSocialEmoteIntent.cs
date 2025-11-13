using Arch.Core;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///
    /// </summary>
    public readonly struct MoveBeforePlayingSocialEmoteIntent
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

        public MoveBeforePlayingSocialEmoteIntent(Vector3 initiatorWorldPosition, Quaternion initiatorWorldRotation, Entity initiatorEntityId, TriggerEmoteReactingToSocialEmoteIntent triggerEmoteIntent)
        {
            InitiatorWorldPosition = initiatorWorldPosition;
            InitiatorWorldRotation = initiatorWorldRotation;
            TriggerEmoteIntent = triggerEmoteIntent;
            InitiatorEntityId = initiatorEntityId;
        }
    }
}
