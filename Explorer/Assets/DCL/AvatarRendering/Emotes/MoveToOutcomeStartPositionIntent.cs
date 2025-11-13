
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    public readonly struct MoveToOutcomeStartPositionIntent
    {
        public readonly Vector3 TargetAvatarPosition;
        public readonly Quaternion TargetAvatarRotation;
        public readonly Vector3 OriginalAvatarPosition;
        public readonly Quaternion OriginalAvatarRotation;
        public readonly TriggerEmoteReactingToSocialEmoteIntent TriggerEmoteIntent;
        public readonly float MovementStartTime;
        public readonly Vector3 InitiatorWorldPosition;

        public MoveToOutcomeStartPositionIntent(Vector3 originalAvatarPosition, Quaternion originalAvatarRotation, Vector3 targetAvatarPosition, Quaternion targetAvatarRotation, TriggerEmoteReactingToSocialEmoteIntent triggerEmoteIntent, Vector3 initiatorWorldPosition)
        {
            TargetAvatarPosition = targetAvatarPosition;
            TargetAvatarRotation = targetAvatarRotation;
            TriggerEmoteIntent = triggerEmoteIntent;
            OriginalAvatarPosition = originalAvatarPosition;
            OriginalAvatarRotation = originalAvatarRotation;
            MovementStartTime = Time.time;
            InitiatorWorldPosition = initiatorWorldPosition;
        }
    }
}
