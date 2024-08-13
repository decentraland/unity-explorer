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
        private const float MAP_MIN_PIN_SCALE = 22;
        private const float MINIMAP_MIN_SIZE_FOR_PIN = 35;

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<PinMarkerObject> poolableBehavior;
        private float currentNewScale;
        private CancellationTokenSource pulseCancellationTokenSource;
        private CancellationTokenSource selectionCancellationTokenSource;

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
            pulseCancellationTokenSource.SafeCancelAndDispose();
            selectionCancellationTokenSource.SafeCancelAndDispose();
        }

        public void SetPosition(Vector2 position, Vector2Int parcelPosition)
        {
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestart();
            IsDestination = false;
            ParcelPosition = parcelPosition;
            poolableBehavior.SetCurrentPosition(position);
        }

        public async UniTaskVoid AnimateSelectionAsync()
        {
            SetIconOutline(true);

            if (poolableBehavior.instance != null)
            {
                selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestart();
                await PinMarkerHelper.ScaleToAsync(poolableBehavior.instance.selectionScalingParent, new Vector2 (1.5f, 1.5f), 0.5f, Ease.OutBack, selectionCancellationTokenSource.Token);
            }
        }

        public async UniTaskVoid AnimateDeselectionAsync()
        {
            SetIconOutline(false);

            if (poolableBehavior.instance != null)
            {
                selectionCancellationTokenSource = selectionCancellationTokenSource.SafeRestart();
                await PinMarkerHelper.ScaleToAsync(poolableBehavior.instance.selectionScalingParent, Vector3.one, 0.5f, Ease.OutBack, selectionCancellationTokenSource.Token);
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
            currentBaseScale = Math.Max(baseScale, MAP_MIN_PIN_SCALE);
            currentNewScale = Math.Max(zoom / baseZoom * currentBaseScale, currentBaseScale);
            poolableBehavior.instance?.SetScale(currentNewScale);
        }

        public void ResetScale()
        {
            currentNewScale = MINIMAP_MIN_SIZE_FOR_PIN;
            poolableBehavior.instance?.SetScale(currentNewScale);
        }

        private void ResetPulseAnimation()
        {
            pulseCancellationTokenSource = pulseCancellationTokenSource.SafeRestart();
            if (!IsDestination && poolableBehavior.instance != null) PinMarkerHelper.PulseScaleAsync(poolableBehavior.instance.pulseScalingParent, ct: pulseCancellationTokenSource.Token).Forget();
        }

        public void Show(Action? onFinish = null)
        {
            poolableBehavior.instance?.SetVisibility(true, onFinish);
        }

        public void Hide(Action? onFinish)
        {
            poolableBehavior.instance?.SetVisibility(false, onFinish);
        }
    }
}
