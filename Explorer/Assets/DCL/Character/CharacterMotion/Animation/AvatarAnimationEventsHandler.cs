using Cysharp.Threading.Tasks;
using DCL.Audio.Avatar;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.Optimization.Pools;
using JetBrains.Annotations;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.CharacterMotion.Animation
{
    public class AvatarAnimationEventsHandler : MonoBehaviour
    {
        [SerializeField] private AvatarAudioPlaybackController AudioPlaybackController;
        [SerializeField] private Animator AvatarAnimator;
        [SerializeField] private float MovementBlendThreshold;
        [SerializeField] private float walkIntervalSeconds = 0.37f;
        [SerializeField] private float jobIntervalSeconds = 0.31f;
        [SerializeField] private float runIntervalSeconds = 0.25f;

        [Header("Feet FX Data")]
        [SerializeField] private Transform leftFootTransform;
        [SerializeField] private Transform rightFootTransform;
        [SerializeField] private ParticleSystem feetDustParticles;
        [SerializeField] private ParticleSystem jumpDustParticles;
        [SerializeField] private ParticleSystem landDustParticles;

        private GameObjectPool<ParticleSystem> feetDustPool = null!;
        private GameObjectPool<ParticleSystem> jumpDustPool = null!;
        private GameObjectPool<ParticleSystem> landDustPool = null!;
        private CancellationTokenSource cancellationTokenSource = null!;

        private float lastFootstepTime;

        private void Start()
        {
            var thisTransform = this.transform;
            feetDustPool = new GameObjectPool<ParticleSystem>(thisTransform, () => Object.Instantiate(feetDustParticles));
            jumpDustPool = new GameObjectPool<ParticleSystem>(thisTransform, () => Object.Instantiate(landDustParticles));
            landDustPool = new GameObjectPool<ParticleSystem>(thisTransform, () => Object.Instantiate(jumpDustParticles));
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            feetDustPool.Dispose();
            jumpDustPool.Dispose();
            landDustPool.Dispose();
        }

        [PublicAPI("Used by Animation Events")]
        public void PlayJumpSound()
        {
            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartWalk);
                    ShowDust(rightFootTransform, jumpDustPool);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartJog);
                    ShowDust(rightFootTransform, jumpDustPool);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpStartRun);
                    ShowDust(rightFootTransform, jumpDustPool);
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
            float currentTime = UnityEngine.Time.time;

            switch (GetMovementState())
            {
                case MovementKind.Walk:
                    if (currentTime - lastFootstepTime > walkIntervalSeconds)
                    {
                        PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepWalk);
                        lastFootstepTime = currentTime;
                        ShowDust(footTransform, feetDustPool);
                    }
                    break;
                case MovementKind.Jog:
                    if (currentTime - lastFootstepTime > jobIntervalSeconds)
                    {
                        PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepJog);
                        lastFootstepTime = currentTime;
                        ShowDust(footTransform, feetDustPool);
                    }
                    break;
                case MovementKind.Run:
                    if (currentTime - lastFootstepTime > runIntervalSeconds)
                    {
                        PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.StepRun);
                        lastFootstepTime = currentTime;
                        ShowDust(footTransform, feetDustPool);
                    }
                    break;
            }
        }

        private void ShowDust(Transform footTransform, IObjectPool<ParticleSystem> dustPool)
        {
            var newDust = dustPool.Get();
            newDust.transform.position = footTransform.position;
            var ct = cancellationTokenSource.Token;
            ReturnDustToPool(newDust, dustPool, ct).Forget();
        }

        private async UniTask ReturnDustToPool(ParticleSystem newDust, IObjectPool<ParticleSystem> dustPool, CancellationToken ct)
        {
            await UniTask.Delay(2000, cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            newDust.Stop();
            newDust.time = 0;
            dustPool.Release(newDust);
        }


        [PublicAPI("Used by Animation Events")]
        public void PlayLandSound()
        {
            switch (GetMovementState())
            {
                case MovementKind.None:
                case MovementKind.Walk:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandWalk);
                    ShowDust(rightFootTransform, landDustPool);
                    break;
                case MovementKind.Jog:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandJog);
                    ShowDust(rightFootTransform, landDustPool);
                    break;
                case MovementKind.Run:
                    PlayAudioForType(AvatarAudioSettings.AvatarAudioClipType.JumpLandRun);
                    ShowDust(rightFootTransform, landDustPool);
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
