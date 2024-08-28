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
        private const float DEFAULT_SCALE_FACTOR = 1.3f;
        private const float DEFAULT_DURATION = .3f;
        private const float DEFAULT_DELAY = .3f;

        public static async UniTaskVoid PulseScaleAsync(Transform transform, float scaleFactor = DEFAULT_SCALE_FACTOR, float duration = DEFAULT_DURATION, float delay = DEFAULT_DELAY, CancellationToken ct = default)
        {
            Vector3 originalScale = Vector3.one;
            Vector3 bigScale = originalScale * scaleFactor;

            while (!ct.IsCancellationRequested)
            {
                await ScaleToAsync(transform, bigScale, duration, Ease.OutBack, CancellationToken.None);
                //we always want to return to the originalScale even if it was canceled
                await ScaleToAsync(transform, originalScale, duration, Ease.OutBack, CancellationToken.None);
                if (!ct.IsCancellationRequested) { await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct); }
            }
            transform.DOKill();
            transform.localScale = originalScale;
        }

        public static async UniTask ScaleToAsync(Transform transform, Vector3 targetScale, float duration, Ease ease, CancellationToken cancellationToken)
        {
            Vector3 originalScale = transform.localScale;

            TweenerCore<Vector3, Vector3, VectorOptions> tween = transform.DOScale(targetScale, duration).SetEase(ease);

            try { await tween.AsyncWaitForCompletion().WithCancellation(cancellationToken); }
            catch (OperationCanceledException)
            {
                transform.localScale = originalScale;
                tween.Kill();
                throw;
            }
        }
    }
}
