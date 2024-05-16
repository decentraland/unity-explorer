using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.CharacterMotion.Animation
{
    public class AvatarAnimationParticlesController : MonoBehaviour, IDisposable
    {
        [SerializeField] private List<AnimationParticlesData> particleSystemPerAnimationTypeList = new ();

        private readonly Dictionary<AvatarAnimationEventType, GameObjectPool<ParticleSystem>> particlePoolsPerAnimationType = new ();

        private CancellationTokenSource cancellationTokenSource = null!;

        private void Start()
        {
            Transform thisTransform = transform;

            foreach (AnimationParticlesData pair in particleSystemPerAnimationTypeList) { particlePoolsPerAnimationType.Add(pair.Type, new GameObjectPool<ParticleSystem>(thisTransform, () => Instantiate(pair.ParticleSystem))); }

            cancellationTokenSource = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();

            foreach (KeyValuePair<AvatarAnimationEventType, GameObjectPool<ParticleSystem>> pair in particlePoolsPerAnimationType) { pair.Value.Dispose(); }

            particleSystemPerAnimationTypeList.Clear();
            particlePoolsPerAnimationType.Clear();
        }

        public void ShowParticles(Transform position, AvatarAnimationEventType animationType)
        {
            if (!particlePoolsPerAnimationType.TryGetValue(animationType, out GameObjectPool<ParticleSystem> particlesPool)) return;

            ParticleSystem? particles = particlesPool.Get();
            particles.transform.position = position.position;
            CancellationToken ct = cancellationTokenSource.Token;
            ScheduleReturnParticlesToPoolAsync(particles, particlesPool, ct).Forget();
        }

        private async UniTask ScheduleReturnParticlesToPoolAsync(ParticleSystem particles, IObjectPool<ParticleSystem> particlesPool, CancellationToken ct)
        {
            await UniTask.Delay(1000, cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            particles.Stop();
            particles.time = 0;
            particlesPool.Release(particles);
        }

        [Serializable]
        private struct AnimationParticlesData
        {
            public AvatarAnimationEventType Type;
            public ParticleSystem ParticleSystem;
        }
    }
}
