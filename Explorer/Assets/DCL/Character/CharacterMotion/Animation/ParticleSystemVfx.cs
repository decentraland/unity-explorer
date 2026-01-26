using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public class ParticleSystemVfx : Vfx<ParticleSystem>
    {
        public ParticleSystemVfx(ParticleSystem target) : base(target)
        {
        }

        public override void OnReleased()
        {
            target.Stop();
            target.time = 0;

            base.OnReleased();
        }

        public override async UniTask WaitForCompletion(CancellationToken ct)
        {
            // Wait the particle system duration so that all particles have been emitted
            await UniTask.Delay((int)(target.main.duration * 1000), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;

            // Now wait till all particles have died off
            while (target.particleCount > 0)
            {
                await UniTask.NextFrame(ct);
                if (ct.IsCancellationRequested) return;
            }
        }
    }
}
