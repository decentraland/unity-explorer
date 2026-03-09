using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.VFX;

namespace DCL.CharacterMotion.Animation
{
    public class VisualEffectVfx : Vfx<VisualEffect>
    {
        private const int VFX_BURST_WAIT_MS = 1000;

        public VisualEffectVfx(VisualEffect target) : base(target)
        {
        }

        public override async UniTask WaitForCompletionAsync(CancellationToken ct)
        {
            // Arbitrary 1-second delay, we assume by this time we emitted all particle bursts
            await UniTask.Delay(VFX_BURST_WAIT_MS, cancellationToken: ct);

            while (target.aliveParticleCount > 0) await UniTask.NextFrame(ct);
        }
    }
}
