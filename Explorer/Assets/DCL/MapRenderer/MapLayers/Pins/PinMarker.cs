using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.Culling;
using System;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Pins
{
    internal class PinMarker : IPinMarker
    {
        internal const int MAX_TITLE_LENGTH = 29;

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<PinMarkerObject> poolableBehavior;
        private float currentBaseScale;
        private float currentNewScale;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public bool IsVisible => poolableBehavior.isVisible;
        public string Title => poolableBehavior.instance.title.text;
        public string Description => poolableBehavior.instance.description.text;
        public Vector2Int ParcelPosition { get; private set; }

        public Vector2 Pivot => new (0.5f, 0.5f);

        internal string title { get; private set; }

        public PinMarker(IObjectPool<PinMarkerObject> objectsPool, IMapCullingController cullingController)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<PinMarkerObject>(objectsPool);
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
            poolableBehavior.SetCurrentPosition(position);
        }

        public void SetTexture(Texture2D texture)
        {
            poolableBehavior.instance.SetTexture(texture);
        }

        public void OnBecameVisible()
        {
            poolableBehavior.OnBecameVisible().title.text = title;

            if(currentBaseScale != 0)
                poolableBehavior.instance.SetScale(currentBaseScale, currentNewScale);
        }

        public void OnBecameInvisible()
        {
            poolableBehavior.OnBecameInvisible();
        }

        public void SetZoom(float baseScale, float baseZoom, float zoom)
        {
            currentBaseScale = baseScale;
            currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);

            if (poolableBehavior.instance != null)
                poolableBehavior.instance.SetScale(currentBaseScale, currentNewScale);
        }

        public void ResetScale(float scale)
        {
            currentNewScale = scale;

            if (poolableBehavior.instance != null)
                poolableBehavior.instance.SetScale(scale, scale);
        }
    }
}
