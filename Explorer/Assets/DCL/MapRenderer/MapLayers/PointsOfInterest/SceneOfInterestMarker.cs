using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.Culling;
using System;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.PointsOfInterest
{
    internal class SceneOfInterestMarker : ISceneOfInterestMarker
    {
        internal const int MAX_TITLE_LENGTH = 29;

        private readonly IMapCullingController cullingController;

        private MapMarkerPoolableBehavior<SceneOfInterestMarkerObject> poolableBehavior;
        private float currentBaseScale;
        private float currentNewScale;

        public Vector3 CurrentPosition => poolableBehavior.currentPosition;

        public bool IsVisible => poolableBehavior.isVisible;

        public Vector2 Pivot => new (0.5f, 0.5f);

        internal string title { get; private set; }

        public SceneOfInterestMarker(IObjectPool<SceneOfInterestMarkerObject> objectsPool, IMapCullingController cullingController)
        {
            poolableBehavior = new MapMarkerPoolableBehavior<SceneOfInterestMarkerObject>(objectsPool);
            this.cullingController = cullingController;
        }

        public void Dispose()
        {
            OnBecameInvisible();
            cullingController.StopTracking(this);
        }

        public void SetData(string title, Vector3 position)
        {
            poolableBehavior.SetCurrentPosition(position);
            this.title = title.Length > MAX_TITLE_LENGTH ? title.Substring(0, MAX_TITLE_LENGTH) : title;
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
