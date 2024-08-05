using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using NBitcoin;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public static class PinMarkerHelper
    {
        public static async UniTask PulseScaleAsync(Transform transform, float scaleFactor = 1.5f, float duration = 0.5f, CancellationToken ct = default)
        {
            Vector3 originalScale = transform.localScale;
            Vector3 bigScale = originalScale * scaleFactor;

            while (!ct.IsCancellationRequested)
            {
                await ScaleToAsync(transform, bigScale, duration, Ease.OutBack, ct);
                if (ct.IsCancellationRequested) break;

                await ScaleToAsync(transform, originalScale, duration, Ease.OutBack, ct);
                if (ct.IsCancellationRequested) break;

                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: ct);
            }

            transform.DOKill();
            transform.localScale = originalScale;
        }

        private static async UniTask ScaleToAsync(Transform transform, Vector3 targetScale, float duration, Ease ease, CancellationToken cancellationToken)
        {
            TweenerCore<Vector3, Vector3, VectorOptions> tween = transform.DOScale(targetScale, duration).SetEase(ease);

            try { await tween.AsyncWaitForCompletion().WithCancellation(cancellationToken); }
            catch (OperationCanceledException)
            {
                tween.Kill();
                throw;
            }
        }
    }
}
