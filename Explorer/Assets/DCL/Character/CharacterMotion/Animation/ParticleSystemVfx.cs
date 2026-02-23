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

        public override async UniTask WaitForCompletionAsync(CancellationToken ct)
        {
            while (target.IsAlive())
            {
                await UniTask.NextFrame(ct);
                if (ct.IsCancellationRequested) return;
            }
        }
    }
}
