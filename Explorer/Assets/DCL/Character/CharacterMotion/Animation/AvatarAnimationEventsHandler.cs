using DCL.Audio.Avatar;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using static DCL.Audio.Avatar.AvatarAudioSettings;

namespace DCL.CharacterMotion.Animation
{
    public enum AvatarAnimationEventType
    {
        Step,
        Jump,
        Land,
    }

    public class AvatarAnimationEventsHandler : MonoBehaviour
    {
        private static readonly Dictionary<(MovementKind, AvatarAnimationEventType), AvatarAudioClipType> AUDIO_CLIP_LOOKUP = new()
        {
            { (MovementKind.RUN, AvatarAnimationEventType.Jump), AvatarAudioClipType.JumpStartRun },
            { (MovementKind.RUN, AvatarAnimationEventType.Land), AvatarAudioClipType.JumpLandRun },
            { (MovementKind.RUN, AvatarAnimationEventType.Step), AvatarAudioClipType.StepRun },
            { (MovementKind.JOG, AvatarAnimationEventType.Jump), AvatarAudioClipType.JumpStartJog },
            { (MovementKind.JOG, AvatarAnimationEventType.Land), AvatarAudioClipType.JumpLandJog },
            { (MovementKind.JOG, AvatarAnimationEventType.Step), AvatarAudioClipType.StepJog },
            { (MovementKind.WALK, AvatarAnimationEventType.Jump), AvatarAudioClipType.JumpStartWalk },
            { (MovementKind.WALK, AvatarAnimationEventType.Land), AvatarAudioClipType.JumpLandWalk },
            { (MovementKind.WALK, AvatarAnimationEventType.Step), AvatarAudioClipType.StepWalk },
            { (MovementKind.IDLE, AvatarAnimationEventType.Jump), AvatarAudioClipType.JumpStartWalk },
            { (MovementKind.IDLE, AvatarAnimationEventType.Land), AvatarAudioClipType.JumpLandWalk },
            { (MovementKind.IDLE, AvatarAnimationEventType.Step), AvatarAudioClipType.StepWalk }
        };

        [SerializeField] private AvatarAudioPlaybackController AudioPlaybackController;
        [SerializeField] private AvatarAnimationParticlesController ParticlesController;
        [SerializeField] private Animator AvatarAnimator;
        [SerializeField] private float MovementBlendThreshold;
        [SerializeField] private float walkIntervalSeconds = 0.37f;
        [SerializeField] private float jogIntervalSeconds = 0.31f;
        [SerializeField] private float runIntervalSeconds = 0.25f;
        [SerializeField] private float jumpIntervalSeconds = 0.25f;
        [SerializeField] private float landIntervalSeconds = 0.25f;

        [Header("Feet FX Data")]
        [SerializeField] private Transform leftFootTransform;
        [SerializeField] private Transform rightFootTransform;
        [SerializeField] private Transform centerBottomTransform;

        private CancellationTokenSource? cancellationTokenSource;
        private float currentTime;

        private float lastFootstepTime;
        private float lastJumpTime;
        private float lastLandTime;
        private bool playingContinuousAudio;

        public event Action? PlayerStepped;

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Jump()
        {
            if (!TryGetAudioClipType(AvatarAnimationEventType.Jump, out var audioClipType)) return;
            if (!TryPlayAnimEventFX(lastJumpTime, jumpIntervalSeconds, centerBottomTransform, AvatarAnimationEventType.Jump,audioClipType)) return;

            lastJumpTime = currentTime;
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Land()
        {
            if (!TryGetAudioClipType(AvatarAnimationEventType.Land, out var audioClipType)) return;
            if (!TryPlayAnimEventFX(lastLandTime, landIntervalSeconds, centerBottomTransform, AvatarAnimationEventType.Land, audioClipType)) return;

            lastLandTime = currentTime;
        }

        private void PlayStepSoundForFoot(Transform footTransform)
        {
            if (!AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) return;

            if (!CheckMovementBlendThreshold()) return;

            if (!TryGetAudioClipType(AvatarAnimationEventType.Step, out var audioClipType)) return;

            float interval = GetIntervalFor(audioClipType);

            if (!TryPlayAnimEventFX(lastFootstepTime, interval, footTransform, AvatarAnimationEventType.Step, audioClipType)) return;

            lastFootstepTime = currentTime;
            PlayerStepped?.Invoke();
            return;

            float GetIntervalFor(AvatarAudioClipType movement) =>
                movement switch
                {
                    AvatarAudioClipType.StepWalk => walkIntervalSeconds,
                    AvatarAudioClipType.StepJog => jogIntervalSeconds,
                    AvatarAudioClipType.StepRun => runIntervalSeconds,
                    _ => 0,
                };
        }

        private bool TryPlayAnimEventFX(float lastPlayedTime, float interval, Transform vfxAttach, AvatarAnimationEventType eventType, AvatarAudioClipType audioClipType)
        {
            currentTime = UnityEngine.Time.time;
            if (currentTime - lastPlayedTime < interval) return false;

            PlaySfxWithParticles(audioClipType, vfxAttach, eventType);
            return true;
        }

                private void PlayContinuousAudio(AvatarAudioClipType clipType)
        {
            AudioPlaybackController.PlayContinuousAudio(clipType);
        }

        private void PlayAudioForType(AvatarAudioClipType clipType)
        {
            AudioPlaybackController.PlayAudioForType(clipType);
        }

        private bool CheckMovementBlendThreshold()
        {
            float movementBlend = AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND);
            return movementBlend > MovementBlendThreshold;
        }

        private bool TryGetAudioClipType(AvatarAnimationEventType eventType, out AvatarAudioClipType audioClipType)
        {
            int movementType = AvatarAnimator.GetInteger(AnimationHashes.MOVEMENT_TYPE);
            var key = ((MovementKind)movementType, eventType);
            return AUDIO_CLIP_LOOKUP.TryGetValue(key, out audioClipType);
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_RightStep()
        {
            PlayStepSoundForFoot(rightFootTransform);
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_LeftStep()
        {
            PlayStepSoundForFoot(leftFootTransform);
        }

        private void PlaySfxWithParticles(AvatarAudioClipType audioClipType, Transform particlesAttach, AvatarAnimationEventType animationEventType)
        {
            PlayAudioForType(audioClipType);
            ParticlesController.ShowParticles(particlesAttach, animationEventType);
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_LongFall() =>
            PlayContinuousAudio(AvatarAudioClipType.LongFall);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_HardLanding()
        {
            PlaySfxWithParticles(AvatarAudioClipType.HardLanding, centerBottomTransform, AvatarAnimationEventType.Land);
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_ShortFall() =>
            PlayAudioForType(AvatarAudioClipType.ShortFall);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_ClothesRustleShort() =>
            PlayAudioForType(AvatarAudioClipType.ClothesRustleShort);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Clap() =>
            PlayAudioForType(AvatarAudioClipType.Clap);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepLight() =>
            PlayAudioForType(AvatarAudioClipType.FootstepLight);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepSlide() =>
            PlayAudioForType(AvatarAudioClipType.FootstepSlide);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepWalkRight() =>
            PlayAudioForType(AvatarAudioClipType.FootstepWalkRight);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_FootstepWalkLeft() =>
            PlayAudioForType(AvatarAudioClipType.FootstepWalkLeft);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Hohoho()
        {
            //In old renderer we would play some sticker animations here,
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_BlowKiss() =>
            PlayAudioForType(AvatarAudioClipType.BlowKiss);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_ThrowMoney() =>
            PlayAudioForType(AvatarAudioClipType.ThrowMoney);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Snowflakes()
        {
            //In old renderer we would play some sticker animations here
        }
    }
}
