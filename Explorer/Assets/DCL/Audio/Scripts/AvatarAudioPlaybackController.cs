using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class AvatarAudioPlaybackController : MonoBehaviour
    {
        [Serializable]
        public enum AvatarAudioClipTypes
        {
            JUMP_START,
            JUMP_RUN_START,
            JUMP_JOG_START,
            STEP_WALK,
            STEP_RUN,
            STEP_JOG,
            JUMP_LAND,
            JUMP_RUN_LAND,
            JUMP_JOG_LAND,
        }

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Animator animator;
        [SerializeField] private AvatarAudioSettings audioSettings;

        private float blendThreshold = 1;

        private void Start()
        {
            blendThreshold = audioSettings.MovementBlendThreshold;
            audioSource.volume = audioSettings.AvatarAudioVolume;
            audioSource.priority = audioSettings.AvatarAudioPriority;
        }

        [UsedImplicitly]
        public void OnJumpStart()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_START).audioClips[0], audioSettings.AvatarAudioVolume);
        }

        [UsedImplicitly]
        public void OnJogJumpStart()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_JOG_START).audioClips[0], audioSettings.AvatarAudioVolume);
        }

        [UsedImplicitly]
        public void OnRunJumpStart()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_RUN_START).audioClips[0], audioSettings.AvatarAudioVolume);
        }

        [UsedImplicitly]
        public void OnRunFootHitGround()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;

            if (animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) && animator.GetBool(AnimationHashes.GROUNDED))
            {
                var audioClipConfig = audioSettings.GetAudioClipForType(AvatarAudioClipTypes.STEP_RUN);
                int randomIndex = Random.Range(0, audioClipConfig.audioClips.Length);
                AudioClip randomClip = audioClipConfig.audioClips[randomIndex];
                audioSource.pitch = 1 + Random.Range(-audioClipConfig.pitchVariation, audioClipConfig.pitchVariation);
                audioSource.PlayOneShot(randomClip, audioSettings.AvatarAudioVolume * audioClipConfig.relativeVolume);
            }
        }

        [UsedImplicitly]
        public void OnWalkFootHitGround()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;

            if (animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold && animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) && animator.GetBool(AnimationHashes.GROUNDED)) { audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.STEP_WALK).audioClips[0], audioSettings.AvatarAudioVolume); }
        }

        [UsedImplicitly]
        public void OnJogFootHitGround()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;

            if (animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) && animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) && animator.GetBool(AnimationHashes.GROUNDED)) { audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.STEP_JOG).audioClips[0], audioSettings.AvatarAudioVolume); }
        }

        [UsedImplicitly]
        public void OnJogJumpFootHitGround()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_JOG_LAND).audioClips[0], audioSettings.AvatarAudioVolume);
        }

        [UsedImplicitly]
        public void OnRunJumpFootHitGround()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_RUN_LAND).audioClips[0], audioSettings.AvatarAudioVolume);
        }

        [UsedImplicitly]
        public void OnJumpFootHitGround()
        {
            if (audioSettings.AvatarAudioVolume <= 0) return;
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_LAND).audioClips[0], audioSettings.AvatarAudioVolume);
        }
    }
}
