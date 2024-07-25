using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public class PathDestinationPin : IPinMarker
    {
        private readonly IMapCullingController cullingController;

        private readonly PinMarkerObject pinMarkerObject;
        private float currentBaseScale;
        private float currentNewScale;
        private CancellationTokenSource cancellationTokenSource;

        public Vector3 CurrentPosition { get; private set; }

        public bool IsVisible { get; }
        public bool IsDestination { get; private set; }

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
            MarkedAsDestination();
        }

        public void AnimateOut()
        {
            IsDestination = false;
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            pinMarkerObject.gameObject.transform.DOScale(0, 0.5f).SetEase(Ease.OutBack);
        }

        public void MarkedAsDestination()
        {
            IsDestination = true;
            pinMarkerObject.SetScale(currentBaseScale, currentNewScale);
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            PinMarkerHelper.PulseScaleAsync(pinMarkerObject.gameObject.transform, ct: cancellationTokenSource.Token).Forget();
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
            if (currentBaseScale != 0)
                pinMarkerObject.SetScale(currentBaseScale, currentNewScale);
        }

        public void OnBecameInvisible()
        {
            pinMarkerObject.SetScale(0, 0);
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            cancellationTokenSource = cancellationTokenSource.SafeRestart();
            currentBaseScale = baseScale;
            currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);
            pinMarkerObject.SetScale(currentBaseScale, currentNewScale);
            PinMarkerHelper.PulseScaleAsync(pinMarkerObject.gameObject.transform, ct: cancellationTokenSource.Token).Forget();
        }

        public void ResetScale(float scale)
        {
            currentNewScale = scale;
            pinMarkerObject.SetScale(scale, scale);
        }
    }
}
