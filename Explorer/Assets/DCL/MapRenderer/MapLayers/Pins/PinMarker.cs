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
        private const float NAVMAP_PIN_DEFAULT_SCALE = 35;
        private const float MINIMAP_MIN_SIZE_FOR_PIN = 45;
        private const float ANIMATION_DURATION = 0.4f;
        private static readonly Vector2 TARGET_ANIMATION_SCALE = new (1.2f, 1.2f);

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<PinMarkerObject> poolableBehavior;
        private float currentNewScale = MINIMAP_MIN_SIZE_FOR_PIN;
        private CancellationTokenSource pulseCancellationTokenSource;
        private CancellationTokenSource selectionCancellationTokenSource;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;
        public Sprite? CurrentSprite => poolableBehavior.instance?.mapPinIcon.sprite;

        public bool IsVisible => poolableBehavior.isVisible;
        public bool IsDestination { get; private set; }
        public bool IsSelected { get; private set; }
        public string Title { get; private set; }

        public string Description { get; private set; }
        public Vector2Int ParcelPosition { get; private set; }

        public Vector2 Pivot => new (0.5f, 0.5f);
        private float currentBaseScale { get; set; }
        private Texture2D? icon { get; set; }

        public PinMarker(IObjectPool<PinMarkerObject> objectsPool, IMapCullingController cullingController)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<PinMarkerObject>(objectsPool);
            this.cullingController = cullingController;
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
            pulseCancellationTokenSource.SafeCancelAndDispose();
            selectionCancellationTokenSource.SafeCancelAndDispose();
        }

        public void SetPosition(Vector2 position, Vector2Int parcelPosition)
        {
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestart();
            ParcelPosition = parcelPosition;
            poolableBehavior.SetCurrentPosition(position);
        }

        public async UniTaskVoid AnimateSelectionAsync(CancellationToken ct)
        {
            SetIconOutline(true);
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestartLinked(ct);
            IsSelected = true;

            if (poolableBehavior.instance != null)
            {
                selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestartLinked(ct);
                await MarkerHelper.ScaleToAsync(poolableBehavior.instance.selectionScalingParent, TARGET_ANIMATION_SCALE, ANIMATION_DURATION, Ease.OutBack, selectionCancellationTokenSource.Token);
            }
        }

        public void DeselectImmediately(IPinMarker.ScaleType scaleType)
        {
            SetIconOutline(false);
            pulseCancellationTokenSource.SafeCancelAndDispose();
            selectionCancellationTokenSource.SafeCancelAndDispose();
            if (poolableBehavior.instance != null)
                poolableBehavior.instance.selectionScalingParent.localScale = Vector3.one;
            ResetScale(scaleType);
            IsSelected = false;
        }

        public async UniTaskVoid AnimateDeselectionAsync(CancellationToken ct)
        {
            SetIconOutline(false);
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestartLinked(ct);
            IsSelected = false;

            if (poolableBehavior.instance != null)
            {
                selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestartLinked(ct);
                await MarkerHelper.ScaleToAsync(poolableBehavior.instance.selectionScalingParent, Vector3.one, 0.5f, Ease.OutBack, selectionCancellationTokenSource.Token);
                //We dont reset the ct in this case because it was already restarted and linked to the ct of AnimateDeselectionAsync
                ResetPulseAnimation(false);
            }
        }

        public void SetAsDestination(bool isDestination)
        {
            IsDestination = isDestination;
            if (poolableBehavior.instance != null)
            {
                poolableBehavior.instance.SetAsDestination(IsDestination);
                ResetPulseAnimation();
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
            IsDestination = false;
        }

        public void SetTexture(Texture2D? texture)
        {
            icon = texture;
            poolableBehavior.instance?.SetTexture(texture);
        }

        public void OnBecameVisible()
        {
            poolableBehavior.OnBecameVisible();
            poolableBehavior.instance?.SetTexture(icon);
            poolableBehavior.instance?.SetAsDestination(IsDestination);
            poolableBehavior.instance?.SetScale(currentNewScale);
            ResetPulseAnimation();
        }

        public void OnBecameInvisible()
        {
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestart();
            poolableBehavior.OnBecameInvisible();
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            currentBaseScale = Math.Max(baseScale, NAVMAP_PIN_DEFAULT_SCALE);
            currentNewScale = Math.Max(zoom / baseZoom * currentBaseScale, currentBaseScale);
            poolableBehavior.instance?.SetScale(currentNewScale);
        }

        public void ResetScale(IPinMarker.ScaleType type)
        {
            currentNewScale = type == IPinMarker.ScaleType.MINIMAP ? MINIMAP_MIN_SIZE_FOR_PIN : NAVMAP_PIN_DEFAULT_SCALE;
            poolableBehavior.instance?.SetScale(currentNewScale);
        }

        private void ResetPulseAnimation(bool resetCt = true)
        {
            if (resetCt) pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            if (!IsDestination && !IsSelected && poolableBehavior.instance != null) MarkerHelper.PulseScaleAsync(poolableBehavior.instance.pulseScalingParent, ct: pulseCancellationTokenSource.Token).Forget();
        }

        public void Show(Action? onFinish = null)
        {
            poolableBehavior.instance?.SetVisibility(true, onFinish);
        }

        public void Hide(Action? onFinish)
        {
            poolableBehavior.instance?.SetVisibility(false, onFinish);
        }

        public GameObject? GetGameObject() =>
            poolableBehavior.instance != null ? poolableBehavior.instance.gameObject : null;

        public Vector2Int? GetParcelPosition() =>
            ParcelPosition;
    }
}
