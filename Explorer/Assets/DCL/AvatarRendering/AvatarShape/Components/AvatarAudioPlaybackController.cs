using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
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

        public void PlayJumpSound()
        {
            switch (GetMovementState())
            {
                case MovementKind.Walk:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartWalk);
                    break;
                case MovementKind.Jog:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartJog);
                    break;
                case MovementKind.Run:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartRun);
                    break;
            }
        }

        public void PlayStepSound()
        {
            if (!AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) return;

            switch (GetMovementState())
            {
                case MovementKind.Walk:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepWalk);
                    break;
                case MovementKind.Jog:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepJog);
                    break;
                case MovementKind.Run:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepRun);
                    break;
            }
        }

        public void PlayLandSound()
        {
            switch (GetMovementState())
            {
                case MovementKind.Walk:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandWalk);
                    break;
                case MovementKind.Jog:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandJog);
                    break;
                case MovementKind.Run:
                    PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandRun);
                    break;
            }
        }

        public void PlayLongFallSound()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.LongFall);
        }

        public void PlayShortFallSound()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.ShortFall);
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

        private MovementKind GetMovementState()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > (int)MovementKind.Jog)
            {
                return MovementKind.Run;
            }

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > (int)(MovementKind.Walk))
            {
                return MovementKind.Jog;
            }

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > blendThreshold)
            {
                return MovementKind.Walk;
            }

            return MovementKind.None;
        }

    }
}
