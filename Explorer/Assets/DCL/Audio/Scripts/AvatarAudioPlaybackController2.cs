using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Audio
{
    public class AvatarAudioPlaybackController2 : MonoBehaviour
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
        public void OnJumpStartA()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpStart);
        }

        [UsedImplicitly]
        public void OnJogJumpStartA()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpJogStart);
        }

        [UsedImplicitly]
        public void OnRunJumpStartA()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpRunStart);
        }

        [UsedImplicitly]
        public void OnRunFootHitGroundA()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioClipType.StepRun); }
        }

        [UsedImplicitly]
        public void OnWalkFootHitGroundA()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk)
                && AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioClipType.StepWalk); }
        }

        [UsedImplicitly]
        public void OnJogFootHitGroundA()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioClipType.StepJog); }
        }

        [UsedImplicitly]
        public void OnJogJumpFootHitGroundA()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpJogLand);
        }

        [UsedImplicitly]
        public void OnRunJumpFootHitGroundA()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpRunLand);
        }

        [UsedImplicitly]
        public void OnJumpFootHitGroundA()
        {
            PlayAvatarAudioForType(AvatarAudioClipType.JumpLand);
        }

        private void PlayAvatarAudioForType(AvatarAudioClipType clipType)
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
