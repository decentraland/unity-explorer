using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using JetBrains.Annotations;
using UnityEngine;
using System.Threading;

namespace DCL.Audio
{
    public class AvatarAudioPlaybackController : MonoBehaviour
    {
        [SerializeField] private AudioSource AvatarAudioSource;
        [SerializeField] private AudioSource ContinuousAudioAvatarAudioSource;
        [SerializeField] private Animator AvatarAnimator;
        [SerializeField] private AvatarAudioSettings AvatarAudioSettings;

        private CancellationTokenSource? cancellationTokenSource;
        private bool playingContinuousAudio;

        private void Start()
        {
            AvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
            ContinuousAudioAvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayJumpSound()
        {
            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartWalk);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartJog);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartRun);
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
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepWalk);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepJog);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepRun);
                    break;
            }
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayLandSound()
        {
            //We stop the looping sounds of the audioSource in case there was any.
            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandWalk);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandJog);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandRun);
                    break;
            }
        }


        [PublicAPI("Used by Animation Events")]
        public void PlayLongFallSound() =>
           PlayContinuousAudio(AvatarAudioSettings.AvatarAudioClipType.LongFall);

        [PublicAPI("Used by Animation Events")]
        public void PlayHardLandingSound() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.HardLanding);

        [PublicAPI("Used by Animation Events")]
        public void PlayShortFallSound() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.ShortFall);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_ClothesRustleShort() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.ClothesRustleShort);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Clap() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.Clap);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepLight() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.FootstepLight);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepWalkRight() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.FootstepWalkRight);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepWalkLeft() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.FootstepWalkLeft);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Hohoho()
        {
            //In old renderer we would play some sticker animations here,
            //we would need to add an animation controller before this that sends either audio events here or shows stickers and whatnot
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_BlowKiss() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.BlowKiss);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_ThrowMoney() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.ThrowMoney);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Snowflakes()
        {
            //In old renderer we would play some sticker animations here
        }

        private void PlayContinuousAudio(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (!AvatarAudioSettings.AudioEnabled) return;

            if (!playingContinuousAudio)
            {
                AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);
                int clipIndex = AudioPlaybackUtilitiesAsync.GetClipIndex(clipConfig);
                ContinuousAudioAvatarAudioSource.volume = clipConfig.RelativeVolume;
                ContinuousAudioAvatarAudioSource.clip = clipConfig.AudioClips[clipIndex];
                ContinuousAudioAvatarAudioSource.Play();
                playingContinuousAudio = true;

                cancellationTokenSource = new CancellationTokenSource();
                var ct = cancellationTokenSource.Token;
                AudioPlaybackUtilitiesAsync.SchedulePlaySound(ct, clipConfig, ContinuousAudioAvatarAudioSource.clip.length, ContinuousAudioAvatarAudioSource).Forget();
            }
        }


        private void PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (playingContinuousAudio)
            {
                playingContinuousAudio = false;
                ContinuousAudioAvatarAudioSource.Stop();
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }

            if (!AvatarAudioSettings.AudioEnabled) return;

            AudioClipConfig clipConfig = AvatarAudioSettings.GetAudioClipConfigForType(clipType);

            if (clipConfig == null)
            {
                ReportHub.LogError(new ReportData(ReportCategory.AUDIO), $"Cannot Play Avatar Audio for {clipType} as it has no AudioClipConfig Assigned");
                return;
            }

            if (clipConfig.AudioClips.Length == 0)
            {
                ReportHub.LogWarning(new ReportData(ReportCategory.AUDIO), $"Cannot Play Avatar Audio for {clipType} as it has no Audio Clips Assigned");
                return;
            }

            if (clipConfig.RelativeVolume == 0)
                return;

            AvatarAudioSource.pitch = AudioPlaybackUtilitiesAsync.GetPitchWithVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilitiesAsync.GetClipIndex(clipConfig);
            AvatarAudioSource.PlayOneShot(clipConfig.AudioClips[clipIndex], clipConfig.RelativeVolume);
        }

        private MovementKind GetMovementState()
        {
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > (int)MovementKind.Jog)
                return MovementKind.Run;

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > (int)MovementKind.Walk)
                return MovementKind.Jog;

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > AvatarAudioSettings.MovementBlendThreshold)
                return MovementKind.Walk;

            return MovementKind.None;
        }
    }
}
