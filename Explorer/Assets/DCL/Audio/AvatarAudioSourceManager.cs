using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using System;
using UnityEngine;

namespace DCL.Audio
{
    public class AvatarAudioSourceManager : MonoBehaviour
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
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_START));
        }
        [UsedImplicitly]
        public void OnJogJumpStart()
        {
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_JOG_START));
        }
        [UsedImplicitly]
        public void OnRunJumpStart()
        {
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_RUN_START));
        }
        [UsedImplicitly]
        public void OnRunFootHitGround()
        {
            if (animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) && animator.GetBool(AnimationHashes.GROUNDED)) { audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.STEP_RUN)); }
        }
        [UsedImplicitly]
        public void OnWalkFootHitGround()
        {
            if (animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold && animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) && animator.GetBool(AnimationHashes.GROUNDED)) { audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.STEP_WALK)); }
        }
        [UsedImplicitly]
        public void OnJogFootHitGround()
        {
            if (animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) && animator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) && animator.GetBool(AnimationHashes.GROUNDED)) { audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.STEP_JOG)); }
        }
        [UsedImplicitly]
        public void OnJogJumpFootHitGround()
        {
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_JOG_LAND));
        }
        [UsedImplicitly]
        public void OnRunJumpFootHitGround()
        {
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_RUN_LAND));
        }
        [UsedImplicitly]
        public void OnJumpFootHitGround()
        {
            audioSource.PlayOneShot(audioSettings.GetAudioClipForType(AvatarAudioClipTypes.JUMP_LAND));
        }
    }
}
