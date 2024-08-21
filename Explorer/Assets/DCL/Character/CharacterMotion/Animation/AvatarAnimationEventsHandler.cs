using DCL.Audio.Avatar;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using System;
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
            var movementState = GetMovementState();
            if (movementState == MovementKind.None) return;

            if (TryPlayAnimEventFX(lastJumpTime, jumpIntervalSeconds, centerBottomTransform, AvatarAnimationEventType.Jump,
                    movementState switch
                    {
                        MovementKind.Jog => AvatarAudioClipType.JumpStartJog,
                        MovementKind.Run => AvatarAudioClipType.JumpStartRun,
                        _ => AvatarAudioClipType.JumpStartWalk
                        }))
                lastJumpTime = currentTime;
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Land()
        {
            var movementState = GetMovementState();
            if (movementState == MovementKind.None) return;

            if (TryPlayAnimEventFX(lastLandTime, landIntervalSeconds, centerBottomTransform, AvatarAnimationEventType.Land,
                    movementState switch
                    {
                        MovementKind.Jog => AvatarAudioClipType.JumpLandJog,
                        MovementKind.Run => AvatarAudioClipType.JumpLandRun,
                        _ => AvatarAudioClipType.JumpLandWalk
                    })) { lastLandTime = currentTime; }
        }

        private void PlayStepSoundForFoot(Transform footTransform)
        {
            if (!AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) return;

            var movementState = GetMovementState();
            if (movementState == MovementKind.None) return;

            float interval = GetIntervalFor(movementState);

            if (TryPlayAnimEventFX(lastFootstepTime, interval, footTransform, AvatarAnimationEventType.Step,
                    movementState switch
                    {
                        MovementKind.Jog => AvatarAudioClipType.StepJog,
                        MovementKind.Run => AvatarAudioClipType.StepRun,
                        _ => AvatarAudioClipType.StepWalk,
                    }))
            {
                lastFootstepTime = currentTime;
                PlayerStepped?.Invoke();
            }

            return;

            float GetIntervalFor(MovementKind movement) =>
                movement switch
                {
                    MovementKind.Walk => walkIntervalSeconds,
                    MovementKind.Jog => jogIntervalSeconds,
                    MovementKind.Run => runIntervalSeconds,
                    MovementKind.None => 0,
                    _ => throw new ArgumentOutOfRangeException(),
                };
        }

        private bool TryPlayAnimEventFX(float lastPlayedTime, float interval, Transform vfxAttach, AvatarAnimationEventType eventType, AvatarAudioClipType audioClipType)
        {
            currentTime = UnityEngine.Time.time;
            if (currentTime - lastPlayedTime < interval) return false;

            PlaySfxWithParticles(audioClipType, vfxAttach, eventType);
            return true;
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

        private void PlayContinuousAudio(AvatarAudioClipType clipType)
        {
            AudioPlaybackController.PlayContinuousAudio(clipType);
        }

        private void PlayAudioForType(AvatarAudioClipType clipType)
        {
            AudioPlaybackController.PlayAudioForType(clipType);
        }

        private MovementKind GetMovementState()
        {
            int movementType = AvatarAnimator.GetInteger(AnimationHashes.MOVEMENT_TYPE);
            float movementBlend = AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND);

            if (movementBlend > MovementBlendThreshold)
            {
                return movementType switch
                       {
                           (int)MovementKind.Run => MovementKind.Run,
                           (int)MovementKind.Jog => MovementKind.Jog,
                           _ => MovementKind.Walk,
                       };
            }

            return MovementKind.None;
        }
    }
}
