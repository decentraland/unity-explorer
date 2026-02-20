using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.VFX;

namespace DCL.CharacterMotion.Animation
{
    public class VisualEffectVfx : Vfx<VisualEffect>
    {
        public VisualEffectVfx(VisualEffect target) : base(target)
        {
        }

        public override async UniTask WaitForCompletionAsync(CancellationToken ct)
        {
            // Arbitrary 1-second delay, we assume by this time we emitted all particle bursts
            await UniTask.Delay(1000, cancellationToken: ct);

            while (target.aliveParticleCount > 0)
            {
                await UniTask.NextFrame(ct);
                if (ct.IsCancellationRequested) return;
            }
        }
    }
}
