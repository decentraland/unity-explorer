using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers
{
    public static class MarkerHelper
    {
        private const float DEFAULT_SCALE_FACTOR = 1.3f;
        private const float DEFAULT_DURATION = .5f;
        private const float DEFAULT_DELAY = 0f;

        public static async UniTaskVoid PulseScaleAsync(Transform transform, float scaleFactor = DEFAULT_SCALE_FACTOR, float duration = DEFAULT_DURATION, float delay = DEFAULT_DELAY, CancellationToken ct = default)
        {
            Vector3 originalScale = Vector3.one;
            Vector3 bigScale = originalScale * scaleFactor;

            while (!ct.IsCancellationRequested)
            {
                await ScaleToAsync(transform, bigScale, duration, Ease.Linear, ct);
                //we always want to return to the originalScale even if it was canceled
                await ScaleToAsync(transform, originalScale, duration, Ease.Linear, CancellationToken.None);
                if (!ct.IsCancellationRequested) { await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct); }
            }
            transform.DOKill();
            transform.localScale = originalScale;
        }

        public static async UniTask ScaleToAsync(Transform transform, Vector3 targetScale, float duration, Ease ease, CancellationToken cancellationToken, Vector3? resetToScale = null)
        {
            Vector3 originalScale = transform.localScale;

            TweenerCore<Vector3, Vector3, VectorOptions> tween = transform.DOScale(targetScale, duration).SetEase(ease);

            try { await tween.AsyncWaitForCompletion().WithCancellation(cancellationToken); }
            catch (OperationCanceledException)
            {
                if (resetToScale != null)
                    transform.localScale = (Vector3) resetToScale;
                else
                    transform.localScale = originalScale;

                tween.Kill();
                throw;
            }
        }

        public static void SetAlpha(SpriteRenderer[] spriteRenderers, TMPro.TextMeshPro[] textMeshes, float alpha)
        {
            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                Color resetColor = sr.color;
                resetColor.a = alpha;
                sr.color = resetColor;
            }

            for (var i = 0; i < textMeshes.Length; i++)
            {
                var tm = textMeshes[i];
                Color resetColor = tm.color;
                resetColor.a = alpha;
                tm.color = resetColor;
            }
        }

        public static async UniTask FadeToAsync(
            SpriteRenderer[] spriteRenderers,
            TMPro.TextMeshPro[] textMeshes,
            float targetAlpha,
            float duration,
            Ease ease,
            CancellationToken cancellationToken,
            float? resetToAlpha = null)
        {
            var originalSpriteAlphas = new float[spriteRenderers.Length];
            var originalTextAlphas = new float[textMeshes.Length];

            for (var i = 0; i < spriteRenderers.Length; i++)
                originalSpriteAlphas[i] = spriteRenderers[i].color.a;

            for (var i = 0; i < textMeshes.Length; i++)
                originalTextAlphas[i] = textMeshes[i].color.a;

            var spriteTweens = new List<TweenerCore<Color, Color, ColorOptions>>(spriteRenderers.Length);
            var textTweens = new List<TweenerCore<Color, Color, ColorOptions>>(textMeshes.Length);

            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                Color targetColor = sr.color;
                targetColor.a = targetAlpha;
                spriteTweens.Add(sr.DOColor(targetColor, duration).SetEase(ease));
            }

            for (var i = 0; i < textMeshes.Length; i++)
            {
                var tm = textMeshes[i];
                Color targetColor = tm.color;
                targetColor.a = targetAlpha;
                textTweens.Add(tm.DOColor(targetColor, duration).SetEase(ease));
            }

            try
            {
                var spriteTasks = new Task[spriteTweens.Count];

                for (var i = 0; i < spriteTweens.Count; i++)
                    spriteTasks[i] = spriteTweens[i].AsyncWaitForCompletion().WithCancellation(cancellationToken);

                var textTasks = new Task[textTweens.Count];

                for (var i = 0; i < textTweens.Count; i++)
                    textTasks[i] = textTweens[i].AsyncWaitForCompletion().WithCancellation(cancellationToken);

                await Task.WhenAll(spriteTasks);
                await Task.WhenAll(textTasks);
            }
            catch (OperationCanceledException)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    var sr = spriteRenderers[i];
                    Color resetColor = sr.color;
                    resetColor.a = resetToAlpha ?? originalSpriteAlphas[i];
                    sr.color = resetColor;
                }

                for (int i = 0; i < textMeshes.Length; i++)
                {
                    var tm = textMeshes[i];
                    Color resetColor = tm.color;
                    resetColor.a = resetToAlpha ?? originalTextAlphas[i];
                    tm.color = resetColor;
                }

                for (var i = 0; i < spriteTweens.Count; i++)
                    spriteTweens[i].Kill();

                for (var i = 0; i < textTweens.Count; i++)
                    textTweens[i].Kill();

                throw;
            }
        }

    }
}
