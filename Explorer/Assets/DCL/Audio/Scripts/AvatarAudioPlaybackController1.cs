using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Audio
{
    public class AvatarAudioPlaybackController1 : MonoBehaviour
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

        public void OnJumpStart()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStart);
        }

        public void OnJogJumpStart()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpJogStart);
        }

        public void OnRunJumpStart()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpRunStart);
        }

        public void OnRunFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepRun); }
        }

        public void OnWalkFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk)
                && AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepWalk); }
        }

        public void OnJogFootHitGround()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Walk) &&
                AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) <= ApplyAnimationMovementBlend.GetMovementBlendId(MovementKind.Jog) &&
                AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) { PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepJog); }
        }

        public void OnJogJumpFootHitGround()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpJogLand);
        }

        public void OnRunJumpFootHitGround()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpRunLand);
        }

        public void OnJumpFootHitGround()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLand);
        }

        private void PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (!AvatarAudioSettings.AudioEnabled) return;

            AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);

            if (clipConfig == null) return;

            AvatarAudioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            AvatarAudioSource.PlayOneShot(clipConfig.AudioClips[clipIndex], clipConfig.RelativeVolume);
        }
    }
}
