using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using UnityEngine;
using Utility.Animations;

namespace DCL.Chat.Teleport
{
    internal static class GotoTeleportEmote
    {
        private const float PLAYBACK_SPEED = 0.65f;
        private const float PEAK_TOLERANCE = 0.005f;
        private static readonly URN EMOTE_URN = new ("fistpump_short");

        public static void Request(World world, Entity player)
        {
            ref CharacterEmoteIntent intent = ref world.AddOrGet(player, new CharacterEmoteIntent());
            intent = new CharacterEmoteIntent { EmoteId = EMOTE_URN, TriggerSource = TriggerSource.Self };
        }

        public static void Apply(World world, Entity player, GotoTeleportState state)
        {
            if (state.Avatar == null) return;

            Animator animator = state.Avatar.AvatarAnimator;
            if (!world.TryGet(player, out CharacterEmoteComponent emote)
                || !emote.EmoteUrn.Equals(EMOTE_URN) || emote.CurrentEmoteReference == null)
            {
                RestoreSpeed(state);
                return;
            }

            AnimatorStateInfo animation = animator.GetCurrentAnimatorStateInfo(AnimatorEmoteLayers.BASE_LAYER_INDEX);
            if (animation.tagHash != AnimationHashes.EMOTE || animator.IsInTransition(AnimatorEmoteLayers.BASE_LAYER_INDEX))
                return;

            state.EmoteClip = emote.CurrentEmoteReference.avatarClip;
            state.EmoteStateHash = animation.fullPathHash;

            if (!state.OwnsAnimatorSpeed)
            {
                state.AnimatorSpeed = animator.speed;
                state.OwnsAnimatorSpeed = true;
                animator.speed = PLAYBACK_SPEED;
            }

            if (state.EmoteFrozen) return;

            Transform avatarTransform = state.Avatar.transform;
            float handHeight = Mathf.Max(
                avatarTransform.InverseTransformPoint(state.Avatar.RightHandAnchorPoint.position).y,
                avatarTransform.InverseTransformPoint(state.Avatar.LeftHandAnchorPoint.position).y);
            float headHeight = avatarTransform.InverseTransformPoint(state.Avatar.HeadAnchorPoint.position).y;

            if (handHeight > state.HighestHandPosition)
            {
                state.HighestHandPosition = handHeight;
                state.HighestHandNormalizedTime = animation.normalizedTime;
            }

            bool passedPeak = state.HighestHandPosition > headHeight
                              && handHeight < state.HighestHandPosition - PEAK_TOLERANCE;
            if (!passedPeak && animation.normalizedTime < 0.85f) return;

            // Rewind to the highest sampled pose and hold it through the departure.
            animator.Play(animation.fullPathHash, AnimatorEmoteLayers.BASE_LAYER_INDEX, state.HighestHandNormalizedTime);
            animator.speed = 0f;
            state.EmoteFrozen = true;
        }

        public static void ApplyLanding(GotoTeleportState state, float progress)
        {
            if (state.Avatar == null || state.EmoteClip == null || !state.EmoteFrozen) return;

            Animator animator = state.Avatar.AvatarAnimator;
            if (!state.LandingEmoteStarted)
            {
                state.Avatar.ReplaceEmoteAnimation(state.EmoteClip);
                state.Avatar.ResetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
                state.Avatar.ResetAnimatorTrigger(AnimationHashes.EMOTE);
                state.Avatar.ResetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
                if (!state.OwnsAnimatorSpeed)
                {
                    state.AnimatorSpeed = animator.speed;
                    state.OwnsAnimatorSpeed = true;
                }

                animator.speed = 0f;
                state.LandingEmoteStarted = true;
            }

            float lowerHand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, progress));
            animator.Play(state.EmoteStateHash, AnimatorEmoteLayers.BASE_LAYER_INDEX,
                Mathf.Lerp(state.HighestHandNormalizedTime, 0f, lowerHand));
        }

        public static void Restore(World world, Entity player, GotoTeleportState state)
        {
            RestoreSpeed(state);
            if (state.LandingEmoteStarted && state.Avatar != null)
                state.Avatar.SetAnimatorTrigger(AnimationHashes.EMOTE_STOP);
            state.LandingEmoteStarted = false;
            state.EmoteClip = null;

            ref CharacterEmoteComponent emote = ref world.TryGetRef<CharacterEmoteComponent>(player, out bool hasEmote);
            if (hasEmote && emote.EmoteUrn.Equals(EMOTE_URN))
                emote.StopEmote = true;

            bool removeIntent = world.TryGet(player, out CharacterEmoteIntent intent) && intent.EmoteId.Equals(EMOTE_URN);
            if (removeIntent)
                world.Remove<CharacterEmoteIntent>(player);
        }

        private static void RestoreSpeed(GotoTeleportState state)
        {
            if (!state.OwnsAnimatorSpeed) return;
            if (state.Avatar != null)
                state.Avatar.AvatarAnimator.speed = state.AnimatorSpeed;
            state.OwnsAnimatorSpeed = false;
        }
    }
}
