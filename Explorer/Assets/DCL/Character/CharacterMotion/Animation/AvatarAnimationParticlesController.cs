using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.CharacterMotion.Animation
{
    public class AvatarAnimationParticlesController : MonoBehaviour, IDisposable
    {
        [SerializeField] private ParticleSystem stepDustParticles;
        [SerializeField] private ParticleSystem jumpDustParticles;
        [SerializeField] private ParticleSystem landDustParticles;

        private GameObjectPool<ParticleSystem> stepDustPool = null!;
        private GameObjectPool<ParticleSystem> jumpDustPool = null!;
        private GameObjectPool<ParticleSystem> landDustPool = null!;
        private CancellationTokenSource cancellationTokenSource = null!;

        private void Start()
        {
            var thisTransform = this.transform;
            stepDustPool = new GameObjectPool<ParticleSystem>(thisTransform, () => Object.Instantiate(stepDustParticles));
            jumpDustPool = new GameObjectPool<ParticleSystem>(thisTransform, () => Object.Instantiate(landDustParticles));
            landDustPool = new GameObjectPool<ParticleSystem>(thisTransform, () => Object.Instantiate(jumpDustParticles));
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void ShowDust(Transform footTransform, AvatarAnimationEventType eventType)
        {
            GameObjectPool<ParticleSystem> dustPool;

            switch (eventType)
            {
                case AvatarAnimationEventType.Step:
                    dustPool = stepDustPool;
                    break;
                case AvatarAnimationEventType.Jump:
                    dustPool = jumpDustPool;
                    break;
                case AvatarAnimationEventType.Land:
                    dustPool = landDustPool;
                    break;
                default: return;
            }

            var newDust = dustPool.Get();
            newDust.transform.position = footTransform.position;
            var ct = cancellationTokenSource.Token;
            ScheduleReturnDustToPoolAsync(newDust, dustPool, ct).Forget();
        }

        private async UniTask ScheduleReturnDustToPoolAsync(ParticleSystem newDust, IObjectPool<ParticleSystem> dustPool, CancellationToken ct)
        {
            await UniTask.Delay(1000, cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            newDust.Stop();
            newDust.time = 0;
            dustPool.Release(newDust);
        }


        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            stepDustPool.Dispose();
            jumpDustPool.Dispose();
            landDustPool.Dispose();
        }
    }
}
