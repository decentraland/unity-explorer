using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.Culling;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal class PinMarker : IPinMarker
    {
        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<PinMarkerObject> poolableBehavior;
        private float currentNewScale;
        private CancellationTokenSource cancellationTokenSource;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;
        public Sprite CurrentSprite => poolableBehavior.instance?.mapPinIcon.sprite;

        public bool IsVisible => poolableBehavior.isVisible;
        public bool IsDestination { get; private set; }
        public string Title { get; private set; }
        public Texture2D Icon { get; private set; }
        public string Description { get; private set; }
        public Vector2Int ParcelPosition { get; private set; }

        public Vector2 Pivot => new (0.5f, 0.5f);
        private float currentBaseScale { get; set; }

        public PinMarker(IObjectPool<PinMarkerObject> objectsPool, IMapCullingController cullingController)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<PinMarkerObject>(objectsPool);
            this.cullingController = cullingController;
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
            cancellationTokenSource.SafeCancelAndDispose();
        }

        public void SetPosition(Vector2 position, Vector2Int parcelPosition)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            ParcelPosition = parcelPosition;
            poolableBehavior.SetCurrentPosition(position);
        }

        public void AnimateIn()
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            poolableBehavior.instance?.gameObject.transform.DOScaleX(poolableBehavior.instance.gameObject.transform.localScale.x * 1.5f, 0.5f).SetEase(Ease.OutBack);
            poolableBehavior.instance?.gameObject.transform.DOScaleY(poolableBehavior.instance.gameObject.transform.localScale.x * 1.5f, 0.5f).SetEase(Ease.OutBack);
            SetIconOutline(true);
        }

        public void AnimateOut()
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            poolableBehavior.instance?.gameObject.transform.DOScaleX(currentNewScale, 0.5f).SetEase(Ease.OutBack);
            poolableBehavior.instance?.gameObject.transform.DOScaleY(currentNewScale, 0.5f).SetEase(Ease.OutBack);
            SetIconOutline(false);
        }

        public void SetAsDestination(bool isDestination)
        {
            IsDestination = isDestination;

            if (isDestination)
            {
                SetScaleAndResetPulse(currentNewScale);
                poolableBehavior.instance.SetAsDestination(true);
            }
            else
            {
                cancellationTokenSource = cancellationTokenSource.SafeRestart();

                if (currentBaseScale != 0) { poolableBehavior.instance?.SetScale(currentNewScale); }
                poolableBehavior.instance?.SetAsDestination(false);
            }
        }

        public void SetIconOutline(bool isActive)
        {
            poolableBehavior.instance?.mapPinIconOutline.gameObject.SetActive(isActive);
        }

        public void SetData(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public void SetTexture(Texture2D texture)
        {
            Icon = texture;
            poolableBehavior.instance?.SetTexture(texture);
        }

        public void OnBecameVisible()
        {
            poolableBehavior.OnBecameVisible();
            if (Icon != null) { poolableBehavior.instance?.SetTexture(Icon); }
            SetScaleAndResetPulse(currentNewScale);
            poolableBehavior.instance?.SetAsDestination(IsDestination);
        }

        public void OnBecameInvisible()
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            poolableBehavior.OnBecameInvisible();
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            currentBaseScale = baseScale;
            currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);

            SetScaleAndResetPulse(currentNewScale);
        }

        public void ResetScale(float scale)
        {
            currentNewScale = scale;
            SetScaleAndResetPulse(scale);
        }

        private void SetScaleAndResetPulse(float newScale)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();

            if (poolableBehavior.instance != null)
            {
                poolableBehavior.instance.SetAsDestination(IsDestination);
                poolableBehavior.instance.SetScale(newScale);
                if (IsDestination) PinMarkerHelper.PulseScaleAsync(poolableBehavior.instance.gameObject.transform, ct: cancellationTokenSource.Token).Forget();
            }
        }
    }
}
