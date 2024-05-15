using DCL.Audio.Avatar;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using UnityEngine;
using System.Threading;

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
        [SerializeField] private float jobIntervalSeconds = 0.31f;
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

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Jump()
        {
            currentTime = UnityEngine.Time.time;
            if (currentTime - lastJumpTime < jumpIntervalSeconds) return;
            lastJumpTime = currentTime;

            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartWalk);
                    ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Jump);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartJog);
                    ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Jump);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartRun);
                    ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Jump);
                    break;
            }
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

        private void PlayStepSoundForFoot(Transform footTransform)
        {
            if (!AvatarAnimator.GetBool(AnimationHashes.GROUNDED)) return;

            currentTime = UnityEngine.Time.time;

            switch (GetMovementState())
            {
                case MovementKind.Walk:
                    if (currentTime - lastFootstepTime > walkIntervalSeconds)
                    {
                        PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepWalk);
                        lastFootstepTime = currentTime;
                        ParticlesController.ShowParticles(footTransform, AvatarAnimationEventType.Step);
                    }

                    break;
                case MovementKind.Jog:
                    if (currentTime - lastFootstepTime > jobIntervalSeconds)
                    {
                        PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepJog);
                        lastFootstepTime = currentTime;
                        ParticlesController.ShowParticles(footTransform, AvatarAnimationEventType.Step);
                    }

                    break;
                case MovementKind.Run:
                    if (currentTime - lastFootstepTime > runIntervalSeconds)
                    {
                        PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepRun);
                        lastFootstepTime = currentTime;
                        ParticlesController.ShowParticles(footTransform, AvatarAnimationEventType.Step);
                    }

                    break;
            }
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_Land()
        {
            currentTime = UnityEngine.Time.time;

            if (currentTime - lastLandTime < landIntervalSeconds) return;

            lastLandTime = currentTime;

            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandWalk);
                    ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Land);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandJog);
                    ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Land);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandRun);
                    ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Land);
                    break;
            }
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_LongFall() =>
            PlayContinuousAudio(AvatarAudioSettings.AvatarAudioClipType.LongFall);

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_HardLanding()
        {
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.HardLanding);
            ParticlesController.ShowParticles(centerBottomTransform, AvatarAnimationEventType.Land);
        }

        [PublicAPI("Used by Animation Events")]
        public void AnimEvent_ShortFall() =>
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
        public void AnimEvent_FootstepSlide() =>
            PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.FootstepSlide);

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
            AudioPlaybackController.PlayContinuousAudio(clipType);
        }

        private void PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType clipType)
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
                           (int)MovementKind.Walk => MovementKind.Walk,
                           _ => MovementKind.None,
                       };
            }

            return MovementKind.None;
        }
    }
}
