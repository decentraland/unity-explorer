using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Audio
{
    public class AvatarAudioPlaybackController : MonoBehaviour
    {
        [FormerlySerializedAs("audioSource")]
        [SerializeField] private AudioSource AvatarAudioSource;
        [FormerlySerializedAs("animator")]
        [SerializeField] private Animator AvatarAnimator;
        [FormerlySerializedAs("audioSettings")]
        [SerializeField] private AvatarAudioSettings AvatarAudioSettings;

        private float blendThreshold = 0.05f;

        private void Start()
        {
            blendThreshold = AvatarAudioSettings.MovementBlendThreshold;
            AvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
        }

        [UsedImplicitly]
        public void OnJumpStart()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpStart);
        }

        [UsedImplicitly]
        public void OnJogJumpStart()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpJogStart);
        }

        [UsedImplicitly]
        public void OnRunJumpStart()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpRunStart);
        }

        [UsedImplicitly]
        public void OnRunFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioClipType.StepRun); }
        }

        [UsedImplicitly]
        public void OnWalkFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk)
                && AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioClipType.StepWalk); }
        }

        [UsedImplicitly]
        public void OnJogFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioClipType.StepJog); }
        }

        [UsedImplicitly]
        public void OnJogJumpFootHitGround()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpJogLand);
        }

        [UsedImplicitly]
        public void OnRunJumpFootHitGround()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpRunLand);
        }

        [UsedImplicitly]
        public void OnJumpFootHitGround()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpLand);
        }

        private void PlayAvatarAudioForType(AvatarAudioClipType clipType, Action check = null)
        {
            if (!AvatarAudioSettings.AudioEnabled) return;

            AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);

            if (clipConfig == null) return;

            AvatarAudioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            AvatarAudioSource.PlayOneShot(clipConfig.AudioClips[clipIndex], clipConfig.RelativeVolume);
        }
    }

    [Serializable]
    public enum AvatarAudioClipType
    {
        JumpStart,
        JumpRunStart,
        JumpJogStart,
        StepWalk,
        StepRun,
        StepJog,
        JumpLand,
        JumpRunLand,
        JumpJogLand,
    }
}
