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
        private const int DEFAULT_AVATAR_AUDIO_PITCH = 1;

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
            AvatarAudioSource.priority = AvatarAudioSettings.AvatarAudioPriority;
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
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED))
            {
                PlayAvatarAudioForType(AvatarAudioClipType.StepRun);
            }
        }

        [UsedImplicitly]
        public void OnWalkFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk)
                && AvatarAnimator.GetBool(AnimationHashes.GROUNDED))
            {
                PlayAvatarAudioForType(AvatarAudioClipType.StepWalk);
            }
        }

        [UsedImplicitly]
        public void OnJogFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED))
            {
                PlayAvatarAudioForType(AvatarAudioClipType.StepJog);
            }
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
            if (!AvatarAudioSettings.AvatarAudioEnabled) return;

            var clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);
            AvatarAudioSource.pitch = DEFAULT_AVATAR_AUDIO_PITCH + AudioPlaybackUtilities.GetPitchVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            AvatarAudioSource.PlayOneShot(clipConfig.audioClips[clipIndex]);
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
