using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using Utility;

namespace DCL.CharacterMotion.Animation
{
    public class AvatarAnimationParticlesController : MonoBehaviour
    {
        [FormerlySerializedAs("particleSystemPerAnimationTypeList")]
        [SerializeField]
        private List<AnimationParticlesData> visualEffects = new ();

        private readonly Dictionary<AvatarAnimationEventType, IObjectPool<IVfx>> poolLookup = new ();

        private CancellationTokenSource cancellationTokenSource = null!;

        private void Start()
        {
            Transform vfxParent = transform;

            foreach (AnimationParticlesData data in visualEffects)
            {
                if (!TryCreatePool(data, vfxParent, out ObjectPool<IVfx> pool)) continue;

                poolLookup.Add(data.Type, pool);
            }

            cancellationTokenSource = new CancellationTokenSource();
        }

        private bool TryCreatePool(AnimationParticlesData data, Transform vfxParent, out ObjectPool<IVfx>? pool)
        {
            Func<IVfx> factory;

            if (data.ParticleSystem != null)
                factory = () => new ParticleSystemVfx(Instantiate(data.ParticleSystem, vfxParent));
            else if (data.VisualEffect != null)
                factory = () => new VisualEffectVfx(Instantiate(data.VisualEffect, vfxParent));
            else
            {
                ReportHub.LogError(ReportCategory.AVATAR, $"Invalid VFX config for {data.Type}");

                pool = null;
                return false;
            }

            pool = new ObjectPool<IVfx>(factory, vfx => vfx.OnSpawn(), vfx => vfx.OnReleased());
            return true;
        }

        private void OnDestroy() =>
            cancellationTokenSource.SafeCancelAndDispose();

        public void PlayVfx(Transform position, AvatarAnimationEventType animationType)
        {
            if (!poolLookup.TryGetValue(animationType, out IObjectPool<IVfx> pool)) return;

            IVfx vfx = pool.Get();
            vfx.SetPosition(position.position);

            ScheduleReturnVfxToPoolAsync(vfx, pool, cancellationTokenSource.Token).Forget();
        }

        private async UniTask ScheduleReturnVfxToPoolAsync(IVfx vfx, IObjectPool<IVfx> pool, CancellationToken ct)
        {
            await vfx.WaitForCompletion(ct);
            
            pool.Release(vfx);
        }

        [Serializable]
        private struct AnimationParticlesData
        {
            public AvatarAnimationEventType Type;

            public ParticleSystem ParticleSystem;

            public VisualEffect VisualEffect;
        }
    }
}
