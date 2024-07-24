using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Pins;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using NBitcoin;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer
{
    public class PathDestinationPin : IPinMarker
    {
        private readonly IMapCullingController cullingController;

        private readonly PinMarkerObject pinMarkerObject;
        private float currentBaseScale;
        private float currentNewScale;
        private CancellationTokenSource cancellationTokenSource;

        public Vector3 CurrentPosition { get; private set; }

        public bool IsVisible { get; private set; }

        public string Title { get; private set; }
        public string Description { get; private set; }
        public Vector2Int ParcelPosition { get; private set; }

        public Vector2 Pivot => new (0.5f, 0.5f);

        internal PathDestinationPin(IMapCullingController cullingController, PinMarkerObject pinMarkerObject)
        {
            this.pinMarkerObject = pinMarkerObject;
            this.cullingController = cullingController;
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
        }

        public void SetPosition(Vector2 position, Vector2Int parcelPosition)
        {
            ParcelPosition = parcelPosition;
            CurrentPosition = position;
            pinMarkerObject.gameObject.transform.position = position;
        }

        public void AnimateIn()
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            PulseScaleAsync(pinMarkerObject.gameObject.transform, ct: cancellationTokenSource.Token).Forget();
        }

        private static async UniTask PulseScaleAsync(Transform transform, float scaleFactor = 1.5f, float duration = 0.5f, CancellationToken ct = default)
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

        public void AnimateOut()
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            pinMarkerObject.gameObject.transform.DOScaleX(currentNewScale, 0.5f).SetEase(Ease.OutBack);
            pinMarkerObject.gameObject.transform.DOScaleY(currentNewScale, 0.5f).SetEase(Ease.OutBack);
            SetIconOutline(false);
        }

        public void SetIconOutline(bool isActive)
        {
            pinMarkerObject.mapPinIconOutline.gameObject.SetActive(isActive);
        }

        public void SetData(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public void SetTexture(Texture2D texture)
        {
            pinMarkerObject.SetTexture(texture);
        }

        public void OnBecameVisible()
        {
            IsVisible = true;

            if (currentBaseScale != 0)
                pinMarkerObject.SetScale(currentBaseScale, currentNewScale);
        }

        public void OnBecameInvisible()
        {
            IsVisible = false;
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            currentBaseScale = baseScale;
            currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);
            pinMarkerObject.SetScale(currentBaseScale, currentNewScale);
            PulseScaleAsync(pinMarkerObject.gameObject.transform, ct: cancellationTokenSource.Token).Forget();
        }

        public void ResetScale(float scale)
        {
            currentNewScale = scale;
            pinMarkerObject.SetScale(scale, scale);
        }
    }
}
