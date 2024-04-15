using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Audio
{
    public class AvatarAudioPlaybackController : MonoBehaviour
    {
        [SerializeField] private AudioSource AvatarAudioSource;
        [SerializeField] private Animator AvatarAnimator;
        [SerializeField] private AvatarAudioSettings AvatarAudioSettings;


        private void Start()
        {
            AvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
        }

        [PublicAPI("Used by Animation Events")]
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

        [PublicAPI("Used by Animation Events")]
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

        [PublicAPI("Used by Animation Events")]
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

        [PublicAPI("Used by Animation Events")]
        public void PlayHardLandingSound()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.HardLanding);

        }

        [PublicAPI("Used by Animation Events")]
        public void PlayLongFallSound()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.LongFall);
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayShortFallSound()
        {
            PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType.ShortFall);
        }

        private void PlayAvatarAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (!AvatarAudioSettings.AudioEnabled) return;

            AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);

            if (clipConfig == null)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Audio for {clipType} as it has no AudioClipConfig Assigned");
                return;
            }

            if (clipConfig.AudioClips.Length == 0)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Avatar Audio for {clipType} as it has no Audio Clips Assigned");
                return;
            }

            if (clipConfig.RelativeVolume == 0) {return;}

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

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > AvatarAudioSettings.MovementBlendThreshold)
            {
                return MovementKind.Walk;
            }

            return MovementKind.None;
        }

    }
}
