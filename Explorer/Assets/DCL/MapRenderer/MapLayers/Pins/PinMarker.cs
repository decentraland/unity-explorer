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
        private const float NAVMAP_PIN_MIN_SCALE = 35;
        private const float MINIMAP_PIN_MIN_SCALE = 45;
        private const float PIN_SIZE_MULTIPLIER = 0.6f;
        private const float ANIMATION_DURATION = 0.4f;
        private static readonly Vector2 TARGET_ANIMATION_SCALE = new (1.2f, 1.2f);

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<PinMarkerObject> poolablePin;
        private float currentTargetScale = MINIMAP_PIN_MIN_SCALE;
        private CancellationTokenSource pulseCancellationTokenSource;
        private CancellationTokenSource selectionCancellationTokenSource;

        public Vector3 CurrentPosition => poolablePin.currentPosition;
        public Sprite? CurrentSprite => poolablePin.instance?.mapPinIcon.sprite;

        public bool IsVisible => poolablePin.isVisible;
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
            poolablePin = new MapMarkerPoolableBehavior<PinMarkerObject>(objectsPool);
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
            poolablePin.SetCurrentPosition(position);
        }

        public async UniTaskVoid AnimateSelectionAsync(CancellationToken ct)
        {
            SetIconOutline(true);
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestartLinked(ct);
            IsSelected = true;

            if (poolablePin.instance != null)
            {
                selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestartLinked(ct);
                await MarkerHelper.ScaleToAsync(poolablePin.instance.selectionScalingParent, TARGET_ANIMATION_SCALE, ANIMATION_DURATION, Ease.OutBack, selectionCancellationTokenSource.Token);
            }
        }

        public void DeselectImmediately(IPinMarker.ScaleType scaleType)
        {
            SetIconOutline(false);
            pulseCancellationTokenSource.SafeCancelAndDispose();
            selectionCancellationTokenSource.SafeCancelAndDispose();
            if (poolablePin.instance != null)
                poolablePin.instance.selectionScalingParent.localScale = Vector3.one;
            ResetScale(scaleType);
            IsSelected = false;
        }

        public async UniTaskVoid AnimateDeselectionAsync(CancellationToken ct)
        {
            SetIconOutline(false);
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestartLinked(ct);
            IsSelected = false;

            if (poolablePin.instance != null)
            {
                selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestartLinked(ct);
                await MarkerHelper.ScaleToAsync(poolablePin.instance.selectionScalingParent, Vector3.one, 0.5f, Ease.OutBack, selectionCancellationTokenSource.Token);
                //We dont reset the ct in this case because it was already restarted and linked to the ct of AnimateDeselectionAsync
                ResetPulseAnimation(false);
            }
        }

        public void SetAsDestination(bool isDestination)
        {
            IsDestination = isDestination;
            if (poolablePin.instance != null)
            {
                poolablePin.instance.SetAsDestination(IsDestination);
                ResetPulseAnimation();
            }
        }

        public void SetIconOutline(bool isActive)
        {
            poolablePin.instance?.mapPinIconOutline.gameObject.SetActive(isActive);
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
            poolablePin.instance?.SetTexture(texture);
        }

        public void OnBecameVisible()
        {
            poolablePin.OnBecameVisible();
            poolablePin.instance?.SetTexture(icon);
            poolablePin.instance?.SetAsDestination(IsDestination);
            poolablePin.instance?.SetScale(currentTargetScale);
            ResetPulseAnimation();
        }

        public void OnBecameInvisible()
        {
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestart();
            poolablePin.OnBecameInvisible();
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            currentBaseScale = Math.Max(baseScale, NAVMAP_PIN_MIN_SCALE);
            currentTargetScale = Math.Max(zoom / baseZoom * currentBaseScale * PIN_SIZE_MULTIPLIER, currentBaseScale);
            poolablePin.instance?.SetScale(currentTargetScale);
        }

        public void ResetScale(IPinMarker.ScaleType type)
        {
            currentTargetScale = type == IPinMarker.ScaleType.MINIMAP ? MINIMAP_PIN_MIN_SCALE : NAVMAP_PIN_MIN_SCALE;
            poolablePin.instance?.SetScale(currentTargetScale);
        }

        private void ResetPulseAnimation(bool resetCt = true)
        {
            if (resetCt) pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            if (!IsDestination && !IsSelected && poolablePin.instance != null) MarkerHelper.PulseScaleAsync(poolablePin.instance.pulseScalingParent, ct: pulseCancellationTokenSource.Token).Forget();
        }

        public void Show(Action? onFinish = null)
        {
            poolablePin.instance?.SetVisibility(true, onFinish);
        }

        public void Hide(Action? onFinish)
        {
            poolablePin.instance?.SetVisibility(false, onFinish);
        }

        public GameObject? GetGameObject() =>
            poolablePin.instance != null ? poolablePin.instance.gameObject : null;

        public Vector2Int? GetParcelPosition() =>
            ParcelPosition;
    }
}
