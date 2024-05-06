using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using JetBrains.Annotations;
using UnityEngine;
using System.Threading;
using Utility;

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
        private bool hasJumped;

        private void Start()
        {
            AvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
            ContinuousAudioAvatarAudioSource.priority = AvatarAudioSettings.AudioPriority;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            ContinuousAudioAvatarAudioSource.Stop();
            cancellationTokenSource?.SafeCancelAndDispose();
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayJumpSound()
        {
            if (hasJumped) return;

            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartWalk);
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play JUMP Sound type {MovementKind.Walk}");
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartJog);
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play JUMP Sound type {MovementKind.Jog}");
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartRun);
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play JUMP Sound type {MovementKind.Run}");
                    break;
            }

            hasJumped = true;
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayStepSound()
        {
            if (!AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) return;

            hasJumped = false;

            switch (GetMovementState())
            {
                case MovementKind.Walk:
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play Step Sound type {MovementKind.Walk}");
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepWalk);
                    break;
                case MovementKind.Jog:
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play Step Sound type {MovementKind.Jog}");
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepJog);
                    break;
                case MovementKind.Run:
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play Step Sound type {MovementKind.Run}");
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepRun);
                    break;
            }
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayLandSound()
        {
            if (!hasJumped) return;

            hasJumped = false;

            //We stop the looping sounds of the audioSource in case there was any.
            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandWalk);
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play LAND Sound type {MovementKind.Walk}");
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandJog);
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play LAND Sound type {MovementKind.Jog}");
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandRun);
                    ReportHub.LogError(new ReportData(ReportCategory.AUDIO),$"Play LAND Sound type {MovementKind.Run}");
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
                int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
                ContinuousAudioAvatarAudioSource.volume = clipConfig.RelativeVolume;
                ContinuousAudioAvatarAudioSource.clip = clipConfig.AudioClips[clipIndex];
                ContinuousAudioAvatarAudioSource.Play();
                playingContinuousAudio = true;

                cancellationTokenSource = new CancellationTokenSource();
                var ct = cancellationTokenSource.Token;
                AudioPlaybackUtilities.SchedulePlaySoundAsync(ct, clipConfig, ContinuousAudioAvatarAudioSource.clip.length, ContinuousAudioAvatarAudioSource).Forget();
            }
        }


        private void PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
        {
            if (playingContinuousAudio)
            {
                playingContinuousAudio = false;
                ContinuousAudioAvatarAudioSource.Stop();
                cancellationTokenSource?.SafeCancelAndDispose();
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

            AvatarAudioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(clipConfig);
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(clipConfig);
            AvatarAudioSource.PlayOneShot(clipConfig.AudioClips[clipIndex], clipConfig.RelativeVolume);
        }

        private MovementKind GetMovementState()
        {
            int movementType = AvatarAnimator.GetInteger(AnimationHashes.MOVEMENT_TYPE);
            float movementBlend = AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND);

            if (movementBlend > AvatarAudioSettings.MovementBlendThreshold)
            {
                return movementType switch
                       {
                           (int)MovementKind.Run => MovementKind.Run,
                           (int)MovementKind.Jog => MovementKind.Jog,
                           (int)MovementKind.Walk => MovementKind.Walk,
                           _ => MovementKind.None
                       };
            }

            return MovementKind.None;
        }
    }
}
