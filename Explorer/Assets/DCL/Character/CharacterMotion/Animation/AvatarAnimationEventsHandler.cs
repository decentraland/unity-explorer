using DCL.Audio.Avatar;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using JetBrains.Annotations;
using UnityEngine;

namespace DCL.DCL.CharacterMotion.Animation
{
    public class AvatarAnimationEventsHandler : MonoBehaviour
    {
        [SerializeField] private AvatarAudioPlaybackController AudioPlaybackController;
        [SerializeField] private Animator AvatarAnimator;
        [SerializeField] private float MovementBlendThreshold;

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
            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > (int)MovementKind.Jog)
                return MovementKind.Run;

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > (int)MovementKind.Walk)
                return MovementKind.Jog;

            if (AvatarAnimator.GetFloat(AnimationHashes.MOVEMENT_BLEND) > MovementBlendThreshold)
                return MovementKind.Walk;

            return MovementKind.None;
        }
    }
}
